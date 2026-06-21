<# .SYNOPSIS Marks a single pending consolidation task as failed. #>
function Invoke-BlockPendingTask {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]       $ContainerName,
        [Parameter(Mandatory)] [string]       $OperationId,
        [Parameter(Mandatory)] [string]       $DbHost,
        [Parameter(Mandatory)] [string]       $DbUser,
        [Parameter(Mandatory)] [SecureString] $DbPassword
    )

    $plainPassword = [System.Net.NetworkCredential]::new('', $DbPassword).Password
    $rowCount = docker exec $ContainerName python /scripts/hindsight/fail_pending.py $OperationId $DbHost $DbUser $plainPassword 2>&1
    [int]$rowCount
}
