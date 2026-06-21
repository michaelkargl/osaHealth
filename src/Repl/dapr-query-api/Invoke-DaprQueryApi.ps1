<#
.SYNOPSIS
Invokes a Dapr Query API spike script (F# .fsx) against a running Dapr sidecar.

.DESCRIPTION
Wraps the three spike scripts so you can run them by name instead of remembering
the dotnet fsi invocation. The framework project is built automatically if its
output DLL is missing.

.PARAMETER Script
The script to invoke. Valid values are the exact filenames (without path):
01-filter-basics, 02-pagination-token, 03-stability-under-insert.
PowerShell provides tab-completion for these values.

.PARAMETER DaprEndpoint
Dapr HTTP endpoint. Default: http://localhost:3500

.PARAMETER StoreName
Dapr state store name. Default: statestore

.PARAMETER WhatIf
Prints what would be invoked without actually running anything.

.EXAMPLE
Invoke-DaprQueryApi -Script 01-filter-basics

.EXAMPLE
Invoke-DaprQueryApi -Script 02-pagination-token -DaprEndpoint http://localhost:3501

.EXAMPLE
Invoke-DaprQueryApi -Script 03-stability-under-insert -WhatIf
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet(
        '01-filter-basics',
        '02-pagination-token',
        '03-stability-under-insert'
    )]
    [string] $Script,

    [Parameter()]
    [string] $DaprEndpoint = 'http://localhost:13500',

    [Parameter()]
    [string] $StoreName = 'statestore'
)

$ErrorActionPreference = 'Stop'

$Header = @{ ForegroundColor = 'Cyan' }
$Info   = @{ ForegroundColor = 'Gray' }

$RepoRoot      = Resolve-Path "$PSScriptRoot\..\..\.."
$FrameworkProj = Join-Path $RepoRoot 'src\osaHealth.Framework\osaHealth.Framework.fsproj'
$FrameworkDll  = Join-Path $RepoRoot 'src\osaHealth.Framework\bin\Debug\net11.0\osaHealth.Framework.dll'

if (-not (Test-Path $FrameworkDll)) {
    Write-Host @Header "Framework DLL not found. Building $FrameworkProj ..."
    if ($PSCmdlet.ShouldProcess($FrameworkProj, 'dotnet build')) {
        dotnet build $FrameworkProj
        if ($LASTEXITCODE -ne 0) {
            throw "Framework build failed (exit code $LASTEXITCODE)."
        }
    }
}

$scriptPath  = Join-Path $PSScriptRoot $Script
$displayName = [System.IO.Path]::GetFileNameWithoutExtension($Script)

Write-Host @Header "`n$('═' * 72)"
Write-Host @Header "  Invoking: $displayName"
Write-Host @Header "$('═' * 72)`n"

$fsiArgs = @($scriptPath, '--daprendpoint', $DaprEndpoint, '--storename', $StoreName)
if ($PSCmdlet.ShouldProcess($scriptPath, "dotnet fsi $fsiArgs")) {
    dotnet fsi @fsiArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$displayName exited with code $LASTEXITCODE."
    }
}
