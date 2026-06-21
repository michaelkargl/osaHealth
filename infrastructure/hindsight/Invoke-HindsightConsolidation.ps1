<#
.SYNOPSIS
    Triggers a Hindsight consolidation run via the HTTP API.

.DESCRIPTION
    Thin wrapper around the Hindsight module. See module help for full documentation:
        Import-Module .\Hindsight; Get-Help Invoke-HindsightConsolidation -Full

.PARAMETER HindsightUrl
    Base URL of the Hindsight API. Defaults to http://localhost:9999.

.PARAMETER BankId
    The Hindsight memory bank to consolidate. Defaults to 'claude_code'.

.EXAMPLE
    .\Invoke-HindsightConsolidation.ps1 -WhatIf

.EXAMPLE
    .\Invoke-HindsightConsolidation.ps1 -BankId my_bank
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [string] $HindsightUrl = 'http://localhost:9999',

    [Parameter()]
    [string] $BankId = 'claude_code'
)

Import-Module "$PSScriptRoot\Hindsight.psd1" -Force

Invoke-HindsightConsolidation @PSBoundParameters
