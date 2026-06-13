<#
.SYNOPSIS
    Blocks pending Hindsight consolidation tasks so they do not run during work hours.

.DESCRIPTION
    Thin wrapper around the Hindsight module. See module help for full documentation:
        Import-Module .\Hindsight; Get-Help Block-HindsightConsolidation -Full

.PARAMETER ContainerName
    Name of the running Hindsight Docker container.

.PARAMETER DbHost
    PostgreSQL host as seen from inside the container. Defaults to 127.0.0.1.

.PARAMETER DbUser
    PostgreSQL username.

.PARAMETER DbPassword
    PostgreSQL password.

.EXAMPLE
    .\Block-HindsightConsolidation.ps1 -WhatIf

.EXAMPLE
    .\Block-HindsightConsolidation.ps1 -DbUser myuser -DbPassword mypassword
#>
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

Import-Module "$PSScriptRoot\Hindsight.psd1" -Force

Block-HindsightConsolidation @PSBoundParameters
