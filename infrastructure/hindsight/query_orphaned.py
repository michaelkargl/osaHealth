"""
query_orphaned.py — Find consolidation tasks stuck in 'processing' after a restart.

Background
----------
Hindsight runs memory consolidation asynchronously. When a consolidation job is
claimed by a worker it is marked status='processing' and stamped with the worker's
ID and a claimed_at timestamp. Under normal operation the worker completes the job
and transitions it to 'completed'.

When the container (or the host machine) is restarted mid-consolidation the worker
process dies without updating the row. The next worker to start sees a row that is
already 'processing' and, because it belongs to a different worker ID, refuses to
claim new work. Consolidation stalls indefinitely — no new memories are consolidated
until the stuck row is resolved.

This script identifies those orphaned rows so that fail_orphaned.py can clear them.
It never writes to the database.

Usage
-----
    python query_orphaned.py <stale_threshold_hours> <db_host> <db_user> <db_password>

    stale_threshold_hours   Rows claimed more than this many hours ago are considered
                            orphaned. Use a non-zero value (e.g. 0.5) to avoid touching
                            jobs that are merely slow rather than dead.

Output
------
One JSON object per orphaned row, printed to stdout, one per line:

    {"operation_id": "...", "worker_id": "...", "claimed_at": "...", "hours_stuck": 1.23}
"""

import json
import sys
from typing import TypedDict

import psycopg2


class OrphanedTask(TypedDict):
    operation_id: str
    worker_id: str
    claimed_at: str
    hours_stuck: float


def find_orphaned_consolidation_tasks(threshold_hours: float, db_host: str, db_user: str, db_password: str) -> list[OrphanedTask]:
    conn = psycopg2.connect(dbname="hindsight", user=db_user, password=db_password, host=db_host)
    cur = conn.cursor()
    cur.execute(
        """
        SELECT
            operation_id,   -- UUID that uniquely identifies this consolidation job
            worker_id,      -- ID of the worker that claimed it (now dead)
            claimed_at::text,

            -- How long the row has been stuck, in hours, rounded to 2 decimal places.
            -- Used for display and to confirm the threshold filter worked correctly.
            ROUND(EXTRACT(EPOCH FROM (NOW() - claimed_at)) / 3600, 2) AS hours_stuck

        FROM async_operations

        WHERE operation_type = 'consolidation'
          AND status = 'processing'

          -- Only rows older than the caller-supplied threshold. This avoids touching
          -- jobs that are genuinely in-flight on a slow but healthy worker.
          AND claimed_at < NOW() - INTERVAL %(threshold)s

        ORDER BY claimed_at  -- oldest first so the caller fixes them in chronological order
        """,
        {"threshold": f"{threshold_hours} hours"},
    )
    tasks = [
        OrphanedTask(
            operation_id=row[0],
            worker_id=row[1],
            claimed_at=row[2],
            hours_stuck=float(row[3]),
        )
        for row in cur.fetchall()
    ]
    conn.close()
    return tasks


def main():
    if len(sys.argv) != 5:
        print("usage: query_orphaned.py <stale_threshold_hours> <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    threshold_hours = float(sys.argv[1])
    db_host, db_user, db_password = sys.argv[2], sys.argv[3], sys.argv[4]

    for task in find_orphaned_consolidation_tasks(threshold_hours, db_host, db_user, db_password):
        print(json.dumps(task))


if __name__ == "__main__":
    main()
