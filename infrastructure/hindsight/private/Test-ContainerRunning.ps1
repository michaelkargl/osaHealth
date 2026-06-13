<# .SYNOPSIS Checks whether the named Docker container is currently running. #>
function Test-ContainerRunning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ContainerName
    )

    $running = docker ps --filter "name=$ContainerName" --format '{{.Names}}' 2>&1
    $running -eq $ContainerName
}
