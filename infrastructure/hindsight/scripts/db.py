"""
db.py — Shared database access for Hindsight maintenance scripts.

Usage
-----
Use HindsightDb as a context manager. The connection is committed and closed
automatically on __exit__; any exception triggers a rollback.

    from db import HindsightDb

    with HindsightDb(db_host, db_user, db_password) as db:
        for task in db.find_orphaned_tasks(threshold_hours=0.5):
            db.fail_task(task.operation_id, status_guard="processing", error_message="...")
"""

from contextlib import AbstractContextManager
from dataclasses import dataclass

import psycopg2


@dataclass
class AsyncOperation:
    operation_id: str
    status: str
    created_at: str
    worker_id: str | None = None
    claimed_at: str | None = None
    hours_stuck: float | None = None
    minutes_waiting: float | None = None


class HindsightDb(AbstractContextManager):
    """Context manager for a single psycopg2 connection to the Hindsight PostgreSQL database."""

    def __init__(self, db_host: str, db_user: str, db_password: str) -> None:
        self._conn = psycopg2.connect(
            dbname="hindsight",
            host=db_host,
            user=db_user,
            password=db_password,
        )

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        if exc_type is None:
            self._conn.commit()
        else:
            self._conn.rollback()
        self._conn.close()

    def find_orphaned_tasks(self, threshold_hours: float) -> list[AsyncOperation]:
        """Return consolidation tasks stuck in 'processing' longer than threshold_hours."""
        cur = self._conn.cursor()
        cur.execute(
            """
            SELECT
                operation_id,
                status,
                created_at::text,
                worker_id,
                claimed_at::text,
                ROUND(EXTRACT(EPOCH FROM (NOW() - claimed_at)) / 3600, 2) AS hours_stuck
            FROM async_operations
            WHERE operation_type = 'consolidation'
              AND status = 'processing'
              AND claimed_at < NOW() - INTERVAL %(threshold)s
            ORDER BY claimed_at
            """,
            {"threshold": f"{threshold_hours} hours"},
        )
        return [
            AsyncOperation(
                operation_id=row[0],
                status=row[1],
                created_at=row[2],
                worker_id=row[3],
                claimed_at=row[4],
                hours_stuck=float(row[5]),
            )
            for row in cur.fetchall()
        ]

    def find_pending_tasks(self) -> list[AsyncOperation]:
        """Return consolidation tasks waiting to be claimed by a worker."""
        cur = self._conn.cursor()
        cur.execute(
            """
            SELECT
                operation_id,
                status,
                created_at::text,
                ROUND(EXTRACT(EPOCH FROM (NOW() - created_at)) / 60, 1) AS minutes_waiting
            FROM async_operations
            WHERE operation_type = 'consolidation'
              AND status = 'pending'
            ORDER BY created_at
            """
        )
        return [
            AsyncOperation(
                operation_id=row[0],
                status=row[1],
                created_at=row[2],
                minutes_waiting=float(row[3]),
            )
            for row in cur.fetchall()
        ]

    def fail_task(self, operation_id: str, *, status_guard: str, error_message: str) -> int:
        """
        Transition operation_id to status='failed'.

        status_guard  — the expected current status; the UPDATE is a no-op if it
                        doesn't match, preventing double-updates from concurrent runs.

        Returns the number of rows updated (0 or 1).
        """
        cur = self._conn.cursor()
        cur.execute(
            """
            UPDATE async_operations
            SET
                status = 'failed',
                error_message = %(error_message)s,
                updated_at = NOW(),
                completed_at = NOW()
            WHERE operation_id = %(operation_id)s
              AND status = %(status_guard)s
            """,
            {
                "operation_id": operation_id,
                "status_guard": status_guard,
                "error_message": error_message,
            },
        )
        return cur.rowcount
