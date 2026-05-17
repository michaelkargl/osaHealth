
$StateStoreBaseUrl = 'http://localhost:3500/v1.0/state/statestore'
$FgGray = @{ ForegroundColor = 'DarkGray' }
$FgGreen = @{ ForegroundColor = 'Green' }
$FgRed = @{ ForegroundColor = 'Red' }

$Expected = @{
    key = 'test'
    value = 'test123'
}

$Body = @"
[
    $($Expected | ConvertTo-Json -Compress)
]
"@

Write-Host @FgGray "Persisting: $Body"
$Body | Invoke-RestMethod `
    -Method Post `
    -Uri "$StateStoreBaseUrl/" `
    -ContentType "application/json"

Write-Host @FgGray "Retrieving value of $($Expected.key)"
$Actual = Invoke-RestMethod `
          -Method Get `
          -Uri "$StateStoreBaseUrl/$($Expected.key)" `
          -ContentType "application/json"


Write-Host
if ( $Actual -eq $Expected.value ) {
    Write-Host @FgGreen ("-"*50)
    Write-Host @FgGreen "✅ Value successfully persisted"
    Write-Host @FgGreen ("-"*50)
} else {
    Write-Host @FgRed ("-"*50)
    Write-Host @FgRed "❌ Expected Value '$($Expected.value)' but got '$($Actual)'"
    Write-Host @FgRed ("-"*50)
}
Write-Host