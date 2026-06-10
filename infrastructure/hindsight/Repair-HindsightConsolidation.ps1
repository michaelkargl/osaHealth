<#
.SYNOPSIS
    Repairs orphaned Hindsight consolidation tasks left behind by a container or machine restart.

.DESCRIPTION
    After a restart, in-flight consolidation tasks remain in 'processing' state
    with a dead worker ID. The new worker sees them as active and refuses to claim
    new work, leaving consolidation permanently stuck.

    This script detects and fails those orphaned tasks so the worker picks up a
    fresh consolidation on its next poll cycle. No memory data is affected — the
    async_operations table is a job queue, not the memory store.

.PARAMETER ContainerName
    Name of the running Hindsight Docker container.

.PARAMETER StaleThresholdHours
    Minimum hours a task must have been in 'processing' before it is considered orphaned.
    Defaults to 0.5 (30 minutes) to avoid touching tasks that are genuinely running.

.PARAMETER DbHost
    PostgreSQL host as seen from inside the container. Defaults to 127.0.0.1.

.PARAMETER DbUser
    PostgreSQL username.

.PARAMETER DbPassword
    PostgreSQL password.

.EXAMPLE
    .\Repair-HindsightConsolidation.ps1 -WhatIf

    Shows which tasks would be failed without making any changes.

.EXAMPLE
    .\Repair-HindsightConsolidation.ps1

    Fails all orphaned consolidation tasks and unblocks the worker.

.EXAMPLE
    .\Repair-HindsightConsolidation.ps1 -ContainerName my-hindsight-1 -StaleThresholdHours 1 -DbUser myuser -DbPassword mypassword
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string] $ContainerName = 'osahealth-hindsight-1',

    [Parameter()]
    [double] $StaleThresholdHours = 0.5,

    [Parameter()]
    [string] $DbHost = '127.0.0.1',

    [Parameter(Mandatory)]
    [string] $DbUser,

    [Parameter(Mandatory)]
    [SecureString] $DbPassword
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-OrphanedTasks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ContainerName,
        [Parameter(Mandatory)] [double] $StaleThresholdHours,
        [Parameter(Mandatory)] [string] $DbHost,
        [Parameter(Mandatory)] [string] $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    $plainPassword = [System.Net.NetworkCredential]::new('', $DbPassword).Password
    $output = docker exec $ContainerName python /scripts/hindsight/query_orphaned.py $StaleThresholdHours $DbHost $DbUser $plainPassword 2>&1
    $output | ForEach-Object {
        if ($_ -match '^\{') { $_ | ConvertFrom-Json }
    }
}

function Invoke-FailOrphanedTask {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ContainerName,
        [Parameter(Mandatory)] [string] $OperationId,
        [Parameter(Mandatory)] [string] $DbHost,
        [Parameter(Mandatory)] [string] $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    $plainPassword = [System.Net.NetworkCredential]::new('', $DbPassword).Password
    $rowCount = docker exec $ContainerName python /scripts/hindsight/fail_orphaned.py $OperationId $DbHost $DbUser $plainPassword 2>&1
    [int]$rowCount
}

function Test-ContainerRunning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ContainerName
    )

    $running = docker ps --filter "name=$ContainerName" --format '{{.Names}}' 2>&1
    $running -eq $ContainerName
}

function Repair-HindsightConsolidation {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)] [string]       $ContainerName,
        [Parameter(Mandatory)] [double]       $StaleThresholdHours,
        [Parameter(Mandatory)] [string]       $DbHost,
        [Parameter(Mandatory)] [string]       $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    if (-not (Test-ContainerRunning -ContainerName $ContainerName)) {
        Write-Error "Container '$ContainerName' is not running. Start it first with: docker compose --profile dev up -d hindsight"
        return
    }

    Write-Host "Checking for orphaned consolidation tasks (stuck > $StaleThresholdHours hours)..."
    $tasks = @(
        Get-OrphanedTasks `
            -ContainerName $ContainerName `
            -StaleThresholdHours $StaleThresholdHours `
            -DbHost $DbHost `
            -DbUser $DbUser `
            -DbPassword $DbPassword
    )

    if (-not $tasks) {
        Write-Host "No orphaned tasks found. Consolidation is either running normally or not yet triggered."
        return
    }

    Write-Host ""
    Write-Host "Found $($tasks.Count) orphaned task(s):"
    $tasks | ForEach-Object {
        Write-Host ("  {0}  worker={1}  claimed={2}  stuck={3}h" -f $_.operation_id, $_.worker_id, $_.claimed_at, $_.hours_stuck)
    }
    Write-Host ""

    $fixed = 0
    foreach ($task in $tasks) {
        if ($PSCmdlet.ShouldProcess($task.operation_id, "Fail orphaned consolidation task")) {
            $rows = Invoke-FailOrphanedTask `
                -ContainerName $ContainerName `
                -OperationId $task.operation_id `
                -DbHost $DbHost `
                -DbUser $DbUser `
                -DbPassword $DbPassword

            if ($rows -eq 1) {
                Write-Host "  Failed: $($task.operation_id)"
                $fixed++
            }
            else {
                Write-Warning "  No rows updated for $($task.operation_id) - may have already been resolved."
            }
        }
    }

    if ($fixed -gt 0) {
        Write-Host ""
        Write-Host "Done. $fixed task(s) cleared. The worker will pick up a fresh consolidation on its next poll (approx 30s)."
    }
}

Repair-HindsightConsolidation `
    -ContainerName $ContainerName `
    -StaleThresholdHours $StaleThresholdHours `
    -DbHost $DbHost `
    -DbUser $DbUser `
    -DbPassword $DbPassword
