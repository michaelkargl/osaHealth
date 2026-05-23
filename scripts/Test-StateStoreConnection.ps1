[CmdletBinding()]
param ()

$FgGray  = @{ ForegroundColor = 'DarkGray' }
$FgGreen = @{ ForegroundColor = 'Green' }
$FgRed   = @{ ForegroundColor = 'Red' }

function Set-PulseReading {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $UserId,
        [Parameter(Mandatory)] [int]    $Bpm,
        [Parameter(Mandatory)] [Int64]  $RecordedAt
    )

    $Key   = "$UserId,$RecordedAt"
    $Value = [PSCustomObject]@{ UserId = $UserId; Bpm = $Bpm; RecordedAt = $RecordedAt }
    $Entry = @{ key = $Key; value = $Value }
    $Body  = "[ $($Entry | ConvertTo-Json -Compress -Depth 100) ]"

    Write-Host @FgGray "Persisting [$Key]: $Body"
    $Body | Invoke-RestMethod `
        -Method Post `
        -Uri 'http://localhost:13500/v1.0/state/statestore' `
        -ContentType "application/json" `
        -Headers @{ 'dapr-app-id' = 'osa-api' }
}

function Get-PulseReadings {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $UserId,
        [Parameter(Mandatory)] [int] $PulseGreaterEqual
    )

    $Query = @{
        filter = @{
            AND = @(
                @{ EQ  = @{ "UserId" = $UserId } }
                @{ GTE = @{ "Bpm"    = $PulseGreaterEqual } }
            )
        }
    } | ConvertTo-Json -Depth 10 -Compress

    Write-Host @FgGray "Querying: $Query"
    Invoke-RestMethod `
        -Method Post `
        -Uri 'http://localhost:13500/v1.0-alpha1/state/statestore/query' `
        -ContentType "application/json" `
        -Headers @{ 'dapr-app-id' = 'osa-api' } `
        -Body $Query
}

# --- seed some readings ---
$UserId = [guid]::NewGuid()
$Now    = [datetime]::UtcNow

Set-PulseReading -UserId $UserId -Bpm 75 -RecordedAt ($Now.AddMinutes(-1).ToFileTimeUtc())
Set-PulseReading -UserId $UserId -Bpm 72 -RecordedAt ($Now.AddMinutes(-2).ToFileTimeUtc())
Set-PulseReading -UserId $UserId -Bpm 80 -RecordedAt  $Now.ToFileTimeUtc()

# --- query them back ---
$Result = Get-PulseReadings -UserId $UserId -PulseGreaterEqual 75
Write-Host @FgGray "Found $($Result.results.Count) item(s): $($Result.results | ConvertTo-Json -Depth 5)"

if ($Result.results.Count -eq 2) {
    Write-Host @FgGreen ("-" * 50)
    Write-Host @FgGreen "All 3 pulse readings persisted and queried successfully"
    Write-Host @FgGreen ("-" * 50)
} else {
    Write-Host @FgRed ("-" * 50)
    Write-Host @FgRed "Expected 3 results but found $($Result.results.Count)"
    Write-Host @FgRed ("-" * 50)
}
Write-Host
