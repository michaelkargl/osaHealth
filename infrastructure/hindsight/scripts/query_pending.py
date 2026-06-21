"""
query_pending.py — List consolidation tasks waiting to be claimed by a worker.

Background
----------
Hindsight creates a row in async_operations with status='pending' each time a
consolidation cycle is triggered. This script is used by Block-HindsightConsolidation.ps1
to identify which tasks to preemptively fail so the worker does not pick them up
during work hours.

It never writes to the database.

Usage
-----
    python query_pending.py <db_host> <db_user> <db_password>

Output
------
One JSON object per pending row, printed to stdout, one per line.
"""

import json
import sys

from db import AsyncOperation, HindsightDb


def main():
    if len(sys.argv) != 4:
        print("usage: query_pending.py <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    db_host, db_user, db_password = sys.argv[1], sys.argv[2], sys.argv[3]

    with HindsightDb(db_host, db_user, db_password) as db:
        tasks: list[AsyncOperation] = db.find_pending_tasks()

    for task in tasks:
        print(json.dumps({
            "operation_id": task.operation_id,
            "created_at": task.created_at,
            "minutes_waiting": task.minutes_waiting,
        }))


if __name__ == "__main__":
    main()
