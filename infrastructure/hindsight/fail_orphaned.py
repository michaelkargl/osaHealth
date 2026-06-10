"""
fail_orphaned.py — Mark a single orphaned consolidation task as failed.

Background
----------
After query_orphaned.py identifies a consolidation job that is stuck in 'processing'
(because its worker died during a restart), this script transitions that row to
status='failed'. The Hindsight worker monitors for failed consolidation jobs and
schedules a fresh one on its next poll cycle (~30 seconds), unblocking consolidation.

No memory data is touched. The async_operations table is a job queue only — it tracks
the lifecycle of background operations but does not store any memory content itself.

Why 'failed' and not 'cancelled' or deleted?
---------------------------------------------
Hindsight's worker explicitly handles 'failed' consolidation rows by scheduling a
retry. Deleting the row or using a different terminal status would bypass that retry
logic and could leave consolidation in an unknown state.

Usage
-----
    python fail_orphaned.py <operation_id> <db_host> <db_user> <db_password>

    operation_id   UUID of the orphaned row, as returned by query_orphaned.py.

Output
------
Prints the number of rows updated (0 or 1) to stdout.

    1   — success, the row was updated
    0   — the row was not found or was no longer 'processing' (already resolved)

The WHERE clause guards against double-updates: if the row has already been resolved
by another means (e.g. a concurrent repair run) the UPDATE simply matches zero rows
and the script exits cleanly.
"""

import sys

import psycopg2


def fail_orphaned_consolidation_task(operation_id: str, db_host: str, db_user: str, db_password: str) -> int:
    conn = psycopg2.connect(dbname="hindsight", user=db_user, password=db_password, host=db_host)
    cur = conn.cursor()
    cur.execute(
        """
        UPDATE async_operations
        SET
            status = 'failed',

            -- Human-readable audit trail so anyone inspecting the table knows
            -- this was an intentional repair, not a genuine processing failure.
            error_message = 'Orphaned by restart - failed by fail_orphaned.py',

            -- Keep updated_at and completed_at consistent with how Hindsight
            -- itself stamps terminal-state transitions.
            updated_at = NOW(),
            completed_at = NOW()

        WHERE operation_id = %(operation_id)s

          -- Safety guard: only update rows that are still 'processing'.
          -- If another repair run or the worker itself already resolved this row,
          -- the UPDATE matches zero rows and we return 0 without raising an error.
          AND status = 'processing'
        """,
        {"operation_id": operation_id},
    )
    rows_updated = cur.rowcount
    conn.commit()
    conn.close()
    return rows_updated


def main():
    if len(sys.argv) != 5:
        print("usage: fail_orphaned.py <operation_id> <db_host> <db_user> <db_password>", file=sys.stderr)
        sys.exit(1)

    operation_id = sys.argv[1]
    db_host, db_user, db_password = sys.argv[2], sys.argv[3], sys.argv[4]

    print(fail_orphaned_consolidation_task(operation_id, db_host, db_user, db_password))


if __name__ == "__main__":
    main()
