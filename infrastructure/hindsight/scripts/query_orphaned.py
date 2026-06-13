"""
query_orphaned.py — Find consolidation tasks stuck in 'processing' after a restart.

Background
----------
When the container (or the host machine) is restarted mid-consolidation the worker
process dies without updating the row. The next worker to start sees a row that is
already 'processing' and refuses to claim new work. Consolidation stalls indefinitely.

This script identifies those orphaned rows so that fail_orphaned.py can clear them.
It never writes to the database.

Usage
-----
    python query_orphaned.py <stale_threshold_hours> <db_host> <db_user> <db_password>

Output
------
One JSON object per orphaned row, printed to stdout, one per line.
"""

import json
import sys

from db import AsyncOperation, HindsightDb


def main():
    if len(sys.argv) != 5:
        print("usage: query_orphaned.py <stale_threshold_hours> <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    threshold_hours = float(sys.argv[1])
    db_host, db_user, db_password = sys.argv[2], sys.argv[3], sys.argv[4]

    with HindsightDb(db_host, db_user, db_password) as db:
        tasks: list[AsyncOperation] = db.find_orphaned_tasks(threshold_hours)

    for task in tasks:
        print(json.dumps({
            "operation_id": task.operation_id,
            "worker_id": task.worker_id,
            "claimed_at": task.claimed_at,
            "hours_stuck": task.hours_stuck,
        }))


if __name__ == "__main__":
    main()
