"""
fail_pending.py — Mark a single pending consolidation task as failed.

Background
----------
During work hours we defer LLM-intensive consolidation until overnight. When
Hindsight creates a consolidation row with status='pending', this script
transitions it to 'failed' before a worker can claim it. A fresh task is
triggered at night by Invoke-HindsightConsolidation.ps1.

No memory data is touched. The async_operations table is a job queue only.

Usage
-----
    python fail_pending.py <operation_id> <db_host> <db_user> <db_password>

Output
------
Prints the number of rows updated (0 or 1) to stdout.
"""

import sys

from db import HindsightDb


def main():
    if len(sys.argv) != 5:
        print("usage: fail_pending.py <operation_id> <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    operation_id = sys.argv[1]
    db_host, db_user, db_password = sys.argv[2], sys.argv[3], sys.argv[4]

    with HindsightDb(db_host, db_user, db_password) as db:
        rows = db.fail_task(
            operation_id,
            status_guard="pending",
            error_message="Blocked during work hours - deferred to overnight by fail_pending.py",
        )

    print(rows)


if __name__ == "__main__":
    main()
