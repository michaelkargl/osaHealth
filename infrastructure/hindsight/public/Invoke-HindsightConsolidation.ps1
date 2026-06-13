<#
.SYNOPSIS
    Triggers a Hindsight consolidation run via the HTTP API.

.DESCRIPTION
    Calls POST /v1/default/banks/{BankId}/consolidate on the Hindsight API,
    which creates a new pending consolidation task. The worker picks it up on
    its next poll cycle (approx 30 seconds).

    Intended to run at the start of the overnight window (e.g. 22:00 via a
    Scheduled Task) after Block-HindsightConsolidation has been suppressing
    consolidation during the day.

.PARAMETER HindsightUrl
    Base URL of the Hindsight API. Defaults to http://localhost:9999.

.PARAMETER BankId
    The Hindsight memory bank to consolidate. Defaults to 'claude_code'.

.EXAMPLE
    Invoke-HindsightConsolidation -WhatIf

    Shows the API call that would be made without executing it.

.EXAMPLE
    Invoke-HindsightConsolidation

    Triggers consolidation immediately using the default bank.

.EXAMPLE
    Invoke-HindsightConsolidation -BankId my_bank -HindsightUrl http://localhost:8888
#>
function Invoke-HindsightConsolidation {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [string] $HindsightUrl = 'http://localhost:9999',

        [Parameter()]
        [string] $BankId = 'claude_code'
    )

    $endpoint = "$HindsightUrl/v1/default/banks/$BankId/consolidate"

    if ($PSCmdlet.ShouldProcess($endpoint, "POST — trigger consolidation")) {
        $response = Invoke-RestMethod -Method POST -Uri $endpoint -ContentType 'application/json'
        Write-Host "Consolidation triggered for bank '$BankId'."
        Write-Host ($response | ConvertTo-Json -Depth 5)
    }
    else {
        Write-Host "Would POST $endpoint"
    }
}
