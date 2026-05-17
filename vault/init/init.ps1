<#
.SYNOPSIS
    Initializes and unseals the osaHealth HashiCorp Vault instance.

.DESCRIPTION
    On first run it initializes Vault, unseals it, enables the kv-v2 secrets
    engine, seeds the MongoDB credentials, and writes the DAPR token file.
    On every subsequent run it unseals Vault with VAULT_UNSEAL_KEY and refreshes
    the DAPR token file.

    Every state-changing operation is gated behind ShouldProcess, so the script
    can be previewed end to end with -WhatIf without touching Vault.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory)]
    [string] $VaultAddr,

    [Parameter(Mandatory)]
    [string] $DaprSecretsPath,

    [Parameter(Mandatory)]
    [string] $MongoUser,

    [Parameter(Mandatory)]
    [string] $MongoPassword,

    [Parameter(Mandatory)]
    [string] $UnsealKey,

    [Parameter(Mandatory)]
    [string] $RootToken,

    [Parameter()]
    [int] $PollIntervalSec = 2
)

<#
.SYNOPSIS
    Sends an HTTP request to the Vault API.

.DESCRIPTION
    Single entry point for all Vault HTTP calls. Constructs the full URI, adds authentication
    headers if a token is provided, and serializes the request body to JSON.

.PARAMETER Path
    API endpoint path relative to the vault server (e.g. 'v1/sys/init', 'v1/sys/unseal').

.PARAMETER Method
    HTTP method: Get, Post, Put, or Delete. Defaults to Get.

.PARAMETER Body
    Optional object to send as JSON-serialized request body.

.PARAMETER Token
    Vault authentication token. If provided, included in the X-Vault-Token header.

.EXAMPLE
    Invoke-VaultApi -Path 'v1/sys/seal-status' -Method Get

.EXAMPLE
    Invoke-VaultApi -Path 'v1/sys/init' -Method Put -Body @{ secret_shares = 1; secret_threshold = 1 }

.EXAMPLE
    Invoke-VaultApi -Path 'v1/sys/unseal' -Method Put -Body @{ key = 'unseal-key-here' } -Token $rootToken
#>
function Invoke-VaultApi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter()]
        [ValidateSet('Get', 'Post', 'Put', 'Delete')]
        [string] $Method = 'Get',

        [Parameter()]
        [object] $Body,

        [Parameter()]
        [string] $Token
    )

    $uri = '{0}/{1}' -f $VaultAddr.TrimEnd('/'), $Path.TrimStart('/')

    $params = @{
        Uri         = $uri
        Method      = $Method
        ErrorAction = 'Stop'
    }
    if ($Token) {
        $params['Headers'] = @{ 'X-Vault-Token' = $Token }
    }
    if ($null -ne $Body) {
        $params['Body']        = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $params['ContentType'] = 'application/json'
    }

    Invoke-RestMethod @params
}

<#
.SYNOPSIS
    Tests whether Vault is reachable.

.DESCRIPTION
    Queries the Vault seal-status endpoint to determine if the server is up and responding.
    Returns $true if reachable (sealed or unsealed), $false if not yet up or unreachable.
#>
function Test-VaultRunning {
    [CmdletBinding()]
    param()
    try {
        Invoke-VaultApi -Path 'v1/sys/seal-status' -Method Get | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

<#
.SYNOPSIS
    Tests whether Vault has already been initialized.

.DESCRIPTION
    Queries the Vault init endpoint to check the initialized flag. Returns $true if Vault
    has been initialized, $false if not yet initialized or if the check fails.
#>
function Test-VaultInitialized {
    [CmdletBinding()]
    param()
    try {
        $status = Invoke-VaultApi -Path 'v1/sys/init' -Method Get
        return [bool] $status.initialized
    }
    catch {
        return $false
    }
}

<#
.SYNOPSIS
    Blocks until Vault becomes reachable.

.DESCRIPTION
    Polls the Vault server repeatedly until it responds to API requests. Skipped under
    -WhatIf so the script stays previewable without a running Vault instance.

.PARAMETER PollIntervalSec
    Seconds between reachability polls. Defaults to 2.
#>
function Wait-ForVault {
    [CmdletBinding()]
    param(
        [Parameter()]
        [int] $PollIntervalSec = 2
    )

    if ($WhatIfPreference) {
        Write-Host "What if: Waiting for Vault at $VaultAddr to become reachable."
        return
    }

    while (-not (Test-VaultRunning)) {
        Write-Host 'Waiting for Vault...'
        Start-Sleep -Seconds $PollIntervalSec
    }
    Write-Host 'Vault is up.'
}

<#
.SYNOPSIS
    Initializes Vault with operator init.

.DESCRIPTION
    Runs Vault's operator init with a single key share, which generates and returns the
    unseal key and root token. Under -WhatIf no key material is generated; placeholders
    are returned instead so the rest of the preview can run.

.EXAMPLE
    $init = Initialize-Vault
    $init.UnsealKey
    $init.RootToken
#>
function Initialize-Vault {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if (-not $PSCmdlet.ShouldProcess($VaultAddr, 'Initialize Vault (operator init, 1 key share)')) {
        return [pscustomobject]@{
            UnsealKey = '<unseal-key-not-generated-in-whatif>'
            RootToken = '<root-token-not-generated-in-whatif>'
        }
    }

    $body     = @{ secret_shares = 1; secret_threshold = 1 }
    $response = Invoke-VaultApi -Path 'v1/sys/init' -Method Put -Body $body

    if (-not $response.keys_base64 -or -not $response.root_token) {
        throw 'Vault init succeeded but the response contained no unseal key or root token.'
    }

    return [pscustomobject]@{
        UnsealKey = $response.keys_base64[0]
        RootToken = $response.root_token
    }
}

<#
.SYNOPSIS
    Submits an unseal key share to Vault.

.DESCRIPTION
    Sends an unseal key share to the Vault unsealing endpoint. After receiving the
    configured threshold of key shares, Vault becomes unsealed.

.PARAMETER Key
    The unseal key share to submit.
#>
function Invoke-VaultUnseal {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string] $Key
    )

    if ($PSCmdlet.ShouldProcess($VaultAddr, 'Unseal Vault')) {
        $response = Invoke-VaultApi -Path 'v1/sys/unseal' -Method Put -Body @{ key = $Key }
        if ($response.sealed) {
            throw 'Vault is still sealed after submitting the unseal key.'
        }
    }
}

<#
.SYNOPSIS
    Enables the kv-v2 secrets engine.

.DESCRIPTION
    Enables Vault's key-value version 2 secrets engine at the specified mount path.

.PARAMETER Token
    Vault authentication token with permission to mount secrets engines.
#>
function Enable-SecretsEngine {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string] $Token,
    )

    if ($PSCmdlet.ShouldProcess("$VaultAddr (secret)", 'Enable kv-v2 secrets engine')) {
        $body = @{ type = 'kv'; options = @{ version = '2' } }
        Invoke-VaultApi -Path "v1/sys/mounts/secret" -Method Post -Body $body -Token $Token | Out-Null
    }
}

<#
.SYNOPSIS
    Writes a secret to Vault's kv-v2 secrets engine.

.DESCRIPTION
    Stores a secret in Vault's kv-v2 secrets engine at the given key under the
    'secret' mount. The value is a hashtable of field names to field values.

.PARAMETER Token
    Vault authentication token with permission to write secrets.

.PARAMETER Key
    The secret path to write to (e.g. 'mongodb').

.PARAMETER Value
    The secret data to write as a hashtable of field names to values.

.EXAMPLE
    Set-VaultSecret -Token $root -Key 'mongodb' -Value @{ username = 'admin'; password = 's3cr3t' }
#>
function Set-VaultSecret {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string] $Token,

        [Parameter(Mandatory)]
        [string] $Key,

        [Parameter(Mandatory)]
        [object] $Value
    )

    if ($PSCmdlet.ShouldProcess("$VaultAddr (secret/$Key)", 'Write vault secret')) {
        $body = @{ data = $Value }
        Invoke-VaultApi -Path "v1/secret/data/$Key" -Method Post -Body $body -Token $Token | Out-Null
    }
}

<#
.SYNOPSIS
    Writes the Vault root token to the DAPR secrets file.

.DESCRIPTION
    Writes the Vault root token as a JSON object to the file that the DAPR
    vault-secret-store component reads. Creates the parent directory if it does not exist.

.PARAMETER Token
    Vault root token to write.

.PARAMETER Path
    File path where the secrets JSON will be written.
#>
function Write-DaprToken {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string] $Token,

        [Parameter(Mandatory)]
        [string] $Path
    )

    if ($PSCmdlet.ShouldProcess($Path, 'Write DAPR secrets file')) {
        $directory = Split-Path -Parent $Path
        if ($directory -and -not (Test-Path -LiteralPath $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        $content = [pscustomobject]@{ vaultToken = $Token } | ConvertTo-Json
        Set-Content -LiteralPath $Path -Value $content -Encoding utf8
    }
}

<#
.SYNOPSIS
    Displays the initialization summary.

.DESCRIPTION
    Prints the Vault unseal key and root token that the operator must copy into
    the .env file after a first-run initialization.

.PARAMETER UnsealKey
    Vault unseal key generated during initialization.

.PARAMETER RootToken
    Vault root token generated during initialization.
#>
function Write-InitSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $UnsealKey,

        [Parameter(Mandatory)]
        [string] $RootToken
    )

    $line = '=' * 60
    Write-Host ''
    Write-Host $line
    Write-Host '  VAULT INITIALIZED'
    Write-Host '  Add these to your .env file, then restart:'
    Write-Host ''
    Write-Host "  VAULT_UNSEAL_KEY=$UnsealKey"
    Write-Host "  VAULT_ROOT_TOKEN=$RootToken"
    Write-Host ''
    Write-Host '  Verify secrets at http://localhost:8200'
    Write-Host $line
    Write-Host ''
}

function Invoke-Main {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Set-StrictMode -Version 3.0
    $ErrorActionPreference = 'Stop'

    Wait-ForVault -PollIntervalSec $PollIntervalSec

    if (-not (Test-VaultInitialized)) {
        Write-Host '=== First run: initializing Vault ==='
        $init = Initialize-Vault
        Invoke-VaultUnseal -Key $init.UnsealKey
        Enable-SecretsEngine -Token $init.RootToken
        Set-VaultSecret -Token $init.RootToken -Key 'mongodb' -Value @{ username = $MongoUser; password = $MongoPassword }
        Write-DaprToken -Token $init.RootToken -Path $DaprSecretsPath
        Write-InitSummary -UnsealKey $init.UnsealKey -RootToken $init.RootToken
    }
    else {
        if (-not $UnsealKey) {
            throw 'Vault is initialized but VAULT_UNSEAL_KEY is not set in .env'
        }
        Invoke-VaultUnseal -Key $UnsealKey
        Write-DaprToken -Token $RootToken -Path $DaprSecretsPath
        Write-Host 'Vault unsealed.'
    }
}

# Run only when executed directly; stay inert when dot-sourced by the tests.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-Main
}
