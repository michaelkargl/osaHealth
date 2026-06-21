<#
.SYNOPSIS
    Repairs orphaned Hindsight consolidation tasks left behind by a container or machine restart.

.DESCRIPTION
    Thin wrapper around the Hindsight module. See module help for full documentation:
        Import-Module .\Hindsight; Get-Help Repair-HindsightConsolidation -Full

.PARAMETER ContainerName
    Name of the running Hindsight Docker container.

.PARAMETER StaleThresholdHours
    Minimum hours a task must have been in 'processing' before it is considered orphaned.
    Defaults to 0.5 (30 minutes).

.PARAMETER DbHost
    PostgreSQL host as seen from inside the container. Defaults to 127.0.0.1.

.PARAMETER DbUser
    PostgreSQL username.

.PARAMETER DbPassword
    PostgreSQL password.

.EXAMPLE
    .\Repair-HindsightConsolidation.ps1 -WhatIf

.EXAMPLE
    .\Repair-HindsightConsolidation.ps1 -DbUser myuser -DbPassword mypassword
#>
[CmdletBinding(SupportsShouldProcess)]
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

Import-Module "$PSScriptRoot\Hindsight.psd1" -Force

Repair-HindsightConsolidation @PSBoundParameters
