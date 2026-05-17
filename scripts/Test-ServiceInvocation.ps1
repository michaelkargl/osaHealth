
$ServiceInvocationBaseUrl = 'http://localhost:3500/v1.0/invoke/osa-api/method'
$FgGray = @{ ForegroundColor = 'DarkGray' }
$FgGreen = @{ ForegroundColor = 'Green' }
$FgRed = @{ ForegroundColor = 'Red' }

$Expected = 'Hello World!'

Write-Host @FgGray "Invoking the osa-api hello world endpoint through the DAPR sidecar"
$Actual = Invoke-RestMethod `
          -Method Get `
          -Uri "$ServiceInvocationBaseUrl/"

Write-Host @FgGray "Received: $Actual"

Write-Host
if ( $Actual -eq $Expected ) {
    Write-Host @FgGreen ("-"*50)
    Write-Host @FgGreen "✅ Service invocation succeeded"
    Write-Host @FgGreen ("-"*50)
} else {
    Write-Host @FgRed ("-"*50)
    Write-Host @FgRed "❌ Expected '$Expected' but got '$Actual'"
    Write-Host @FgRed ("-"*50)
}
Write-Host
