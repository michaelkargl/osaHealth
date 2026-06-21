<# .SYNOPSIS Returns consolidation tasks stuck in 'processing' beyond the stale threshold. #>
function Get-OrphanedTasks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]       $ContainerName,
        [Parameter(Mandatory)] [double]       $StaleThresholdHours,
        [Parameter(Mandatory)] [string]       $DbHost,
        [Parameter(Mandatory)] [string]       $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    $plainPassword = [System.Net.NetworkCredential]::new('', $DbPassword).Password
    $output = docker exec $ContainerName python /scripts/hindsight/query_orphaned.py $StaleThresholdHours $DbHost $DbUser $plainPassword 2>&1
    $output | ForEach-Object {
        if ($_ -match '^\{') { $_ | ConvertFrom-Json }
    }
}
