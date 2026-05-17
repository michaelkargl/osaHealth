
$ServiceInvocationBaseUrl = 'http://localhost:13500/v1.0/invoke/osa-api/method'
$FgGray = @{ ForegroundColor = 'DarkGray' }
$FgGreen = @{ ForegroundColor = 'Green' }
$FgRed = @{ ForegroundColor = 'Red' }

Write-Host @FgGray "Invoking the osa-api /health endpoint through the DAPR sidecar"
$Response = Invoke-WebRequest `
            -Method Get `
            -Uri "$ServiceInvocationBaseUrl/health"

Write-Host @FgGray "Received HTTP $($Response.StatusCode): $($Response.Content)"

Write-Host
if ( $Response.StatusCode -eq 200 ) {
    Write-Host @FgGreen ("-"*50)
    Write-Host @FgGreen "✅ Health check succeeded"
    Write-Host @FgGreen ("-"*50)
} else {
    Write-Host @FgRed ("-"*50)
    Write-Host @FgRed "❌ Expected HTTP 200 but got HTTP $($Response.StatusCode)"
    Write-Host @FgRed ("-"*50)
}
Write-Host
