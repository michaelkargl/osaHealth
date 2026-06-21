<# .SYNOPSIS Returns pending consolidation tasks waiting to be claimed. #>
function Get-PendingTasks {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]       $ContainerName,
        [Parameter(Mandatory)] [string]       $DbHost,
        [Parameter(Mandatory)] [string]       $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    $plainPassword = [System.Net.NetworkCredential]::new('', $DbPassword).Password
    $output = docker exec $ContainerName python /scripts/hindsight/query_pending.py $DbHost $DbUser $plainPassword 2>&1
    $output | ForEach-Object {
        if ($_ -match '^\{') { $_ | ConvertFrom-Json }
    }
}
