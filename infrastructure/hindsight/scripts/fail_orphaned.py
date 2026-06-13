"""
fail_orphaned.py — Mark a single orphaned consolidation task as failed.

Background
----------
After query_orphaned.py identifies a consolidation job stuck in 'processing'
(because its worker died during a restart), this script transitions that row to
status='failed'. The Hindsight worker schedules a fresh consolidation on its next
poll cycle (~30 seconds), unblocking consolidation.

No memory data is touched. The async_operations table is a job queue only.

Usage
-----
    python fail_orphaned.py <operation_id> <db_host> <db_user> <db_password>

Output
------
Prints the number of rows updated (0 or 1) to stdout.
"""

import sys

from db import HindsightDb


def main():
    if len(sys.argv) != 5:
        print("usage: fail_orphaned.py <operation_id> <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    operation_id = sys.argv[1]
    db_host, db_user, db_password = sys.argv[2], sys.argv[3], sys.argv[4]

    with HindsightDb(db_host, db_user, db_password) as db:
        rows = db.fail_task(
            operation_id,
            status_guard="processing",
            error_message="Orphaned by restart - failed by fail_orphaned.py",
        )

    print(rows)


if __name__ == "__main__":
    main()
