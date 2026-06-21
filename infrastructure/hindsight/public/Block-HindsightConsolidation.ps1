<#
.SYNOPSIS
    Blocks pending Hindsight consolidation tasks so they do not run during work hours.

.DESCRIPTION
    Hindsight triggers consolidation automatically throughout the day. Each trigger
    creates a row with status='pending' in async_operations. This script preemptively
    marks those rows as 'failed' before a worker can claim them, deferring all LLM
    consolidation cost to overnight.

    Run this periodically during work hours (e.g. every 5 minutes via a Scheduled Task)
    to keep consolidation suppressed. Pair with Invoke-HindsightConsolidation to
    trigger a fresh run at the start of the overnight window.

    No memory data is affected — async_operations is a job queue, not the memory store.

.PARAMETER ContainerName
    Name of the running Hindsight Docker container.

.PARAMETER DbHost
    PostgreSQL host as seen from inside the container. Defaults to 127.0.0.1.

.PARAMETER DbUser
    PostgreSQL username.

.PARAMETER DbPassword
    PostgreSQL password.

.EXAMPLE
    Block-HindsightConsolidation -DbUser myuser -DbPassword (Read-Host -AsSecureString) -WhatIf

    Shows which pending tasks would be blocked without making any changes.

.EXAMPLE
    Block-HindsightConsolidation -DbUser myuser -DbPassword (Read-Host -AsSecureString)

    Fails all pending consolidation tasks immediately.
#>
function Block-HindsightConsolidation {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [string] $ContainerName = 'osahealth-hindsight-1',

        [Parameter()]
        [string] $DbHost = '127.0.0.1',

        [Parameter(Mandatory)]
        [string] $DbUser,

        [Parameter(Mandatory)]
        [SecureString] $DbPassword
    )

    if (-not (Test-ContainerRunning -ContainerName $ContainerName)) {
        Write-Error "Container '$ContainerName' is not running."
        return
    }

    Write-Host "Checking for pending consolidation tasks..."
    $tasks = @(
        Get-PendingTasks `
            -ContainerName $ContainerName `
            -DbHost $DbHost `
            -DbUser $DbUser `
            -DbPassword $DbPassword
    )

    if (-not $tasks) {
        Write-Host "No pending consolidation tasks. Nothing to block."
        return
    }

    Write-Host ""
    Write-Host "Found $($tasks.Count) pending task(s):"
    $tasks | ForEach-Object {
        Write-Host ("  {0}  created={1}  waiting={2}m" -f $_.operation_id, $_.created_at, $_.minutes_waiting)
    }
    Write-Host ""

    $blocked = 0
    foreach ($task in $tasks) {
        if ($PSCmdlet.ShouldProcess($task.operation_id, "Block pending consolidation task")) {
            $rows = Invoke-BlockPendingTask `
                -ContainerName $ContainerName `
                -OperationId $task.operation_id `
                -DbHost $DbHost `
                -DbUser $DbUser `
                -DbPassword $DbPassword

            if ($rows -eq 1) {
                Write-Host "  Blocked: $($task.operation_id)"
                $blocked++
            }
            else {
                Write-Warning "  No rows updated for $($task.operation_id) - worker may have already claimed it."
            }
        }
    }

    if ($blocked -gt 0) {
        Write-Host ""
        Write-Host "Done. $blocked task(s) blocked. Consolidation deferred until overnight."
    }
}
