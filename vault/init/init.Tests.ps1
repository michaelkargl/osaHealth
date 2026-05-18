# Pester tests for vault/init/init.ps1
#
# Run with:  Invoke-Pester -Path ./vault/init -Output Detailed
# Requires Pester 5+.

BeforeAll {
    # Dot-sourcing makes the script's functions available without running
    # Invoke-Main (see the InvocationName guard at the bottom of init.ps1).
    # Dummy values satisfy the mandatory parameters; individual tests override as needed.
    . (Join-Path $PSScriptRoot 'init.ps1') `
        -VaultBaseUri    'http://vault:8200' `
        -DaprSecretsPath '/tmp/test-secrets.json' `
        -MongoUser       'testuser' `
        -MongoPassword   'testpass' `
        -UnsealKey       '' `
        -RootToken       ''
}

Describe 'Test-VaultRunning' {
    It 'returns $true when the seal-status endpoint responds' {
        Mock Invoke-VaultApi { [pscustomobject]@{ sealed = $true } }
        Test-VaultRunning -VaultBaseUri 'http://vault:8200' | Should -BeTrue
    }

    It 'returns $false when the endpoint is unreachable' {
        Mock Invoke-VaultApi { throw 'connection refused' }
        Test-VaultRunning -VaultBaseUri 'http://vault:8200' | Should -BeFalse
    }
}

Describe 'Test-VaultInitialized' {
    It 'returns $true when Vault reports it is initialized' {
        Mock Invoke-VaultApi { [pscustomobject]@{ initialized = $true } }
        Test-VaultInitialized -VaultBaseUri 'http://vault:8200' | Should -BeTrue
    }

    It 'returns $false when Vault reports it is not initialized' {
        Mock Invoke-VaultApi { [pscustomobject]@{ initialized = $false } }
        Test-VaultInitialized -VaultBaseUri 'http://vault:8200' | Should -BeFalse
    }

    It 'returns $false when the endpoint is unreachable' {
        Mock Invoke-VaultApi { throw 'connection refused' }
        Test-VaultInitialized -VaultBaseUri 'http://vault:8200' | Should -BeFalse
    }
}

Describe 'Wait-ForVault' {
    It 'polls until Vault becomes reachable' {
        $attempts = [ref] 0
        Mock Test-VaultRunning { $attempts.Value++; return ($attempts.Value -ge 3) }
        Mock Start-Sleep { }

        Wait-ForVault -VaultBaseUri 'http://vault:8200' -PollIntervalSec 0

        Should -Invoke Test-VaultRunning -Times 3 -Exactly
    }

    It 'skips waiting entirely in -WhatIf mode' {
        Mock Test-VaultRunning { throw 'should not be reached in WhatIf mode' }
        $WhatIfPreference = $true

        { Wait-ForVault -VaultBaseUri 'http://vault:8200' } | Should -Not -Throw

        Should -Invoke Test-VaultRunning -Times 0 -Exactly
    }
}

Describe 'Initialize-Vault' {
    It 'returns the unseal key and root token from the API response' {
        Mock Invoke-VaultApi {
            [pscustomobject]@{ keys_base64 = @('unseal-abc'); root_token = 'hvs.root123' }
        }

        $result = Initialize-Vault -VaultBaseUri 'http://vault:8200'

        $result.UnsealKey | Should -Be 'unseal-abc'
        $result.RootToken | Should -Be 'hvs.root123'
        Should -Invoke Invoke-VaultApi -Times 1 -Exactly
    }

    It 'throws when the response carries no key material' {
        Mock Invoke-VaultApi { [pscustomobject]@{ keys_base64 = @(); root_token = '' } }
        { Initialize-Vault -VaultBaseUri 'http://vault:8200' } | Should -Throw
    }

    It 'does not call the API in -WhatIf mode' {
        Mock Invoke-VaultApi { throw 'API must not be called under WhatIf' }

        $result = Initialize-Vault -VaultBaseUri 'http://vault:8200' -WhatIf

        Should -Invoke Invoke-VaultApi -Times 0 -Exactly
        $result.UnsealKey | Should -Not -BeNullOrEmpty
        $result.RootToken | Should -Not -BeNullOrEmpty
    }
}

Describe 'Invoke-VaultUnseal' {
    It 'submits the unseal key to the API' {
        Mock Invoke-VaultApi { [pscustomobject]@{ sealed = $false } }

        Invoke-VaultUnseal -VaultBaseUri 'http://vault:8200' -Key 'unseal-abc'

        Should -Invoke Invoke-VaultApi -Times 1 -Exactly -ParameterFilter {
            $Path -eq 'v1/sys/unseal' -and $Method -eq 'Put'
        }
    }

    It 'throws when Vault is still sealed afterwards' {
        Mock Invoke-VaultApi { [pscustomobject]@{ sealed = $true } }
        { Invoke-VaultUnseal -VaultBaseUri 'http://vault:8200' -Key 'bad-key' } | Should -Throw
    }

    It 'does not call the API in -WhatIf mode' {
        Mock Invoke-VaultApi { throw 'API must not be called under WhatIf' }
        Invoke-VaultUnseal -VaultBaseUri 'http://vault:8200' -Key 'unseal-abc' -WhatIf
        Should -Invoke Invoke-VaultApi -Times 0 -Exactly
    }
}

Describe 'Enable-SecretsEngine' {
    It 'enables kv-v2 at the secret mount path' {
        Mock Invoke-VaultApi { }

        Enable-SecretsEngine -VaultBaseUri 'http://vault:8200' -Token 'hvs.root'

        Should -Invoke Invoke-VaultApi -Times 1 -Exactly -ParameterFilter {
            $Path -eq 'v1/sys/mounts/secret' -and $Method -eq 'Post'
        }
    }

    It 'does not call the API in -WhatIf mode' {
        Mock Invoke-VaultApi { throw 'API must not be called under WhatIf' }
        Enable-SecretsEngine -VaultBaseUri 'http://vault:8200' -Token 'hvs.root' -WhatIf
        Should -Invoke Invoke-VaultApi -Times 0 -Exactly
    }
}

Describe 'Set-VaultSecret' {
    It 'writes a secret to the kv-v2 secrets engine at the given path' {
        Mock Invoke-VaultApi { }

        Set-VaultSecret -VaultBaseUri 'http://vault:8200' -Token 'hvs.root' -Key 'mongodb' -Value @{ username = 'admin'; password = 's3cret' }

        Should -Invoke Invoke-VaultApi -Times 1 -Exactly -ParameterFilter {
            $Path -eq 'v1/secret/data/mongodb' -and $Method -eq 'Post'
        }
    }

    It 'does not call the API in -WhatIf mode' {
        Mock Invoke-VaultApi { throw 'API must not be called under WhatIf' }
        Set-VaultSecret -VaultBaseUri 'http://vault:8200' -Token 'hvs.root' -Key 'mongodb' -Value @{ username = 'admin'; password = 's3cret' } -WhatIf
        Should -Invoke Invoke-VaultApi -Times 0 -Exactly
    }
}

Describe 'Write-DaprToken' {
    It 'writes a JSON file containing the vault token' {
        $path = Join-Path $TestDrive 'secrets.json'

        Write-DaprToken -Token 'hvs.xyz' -Path $path

        Test-Path $path | Should -BeTrue
        (Get-Content -Raw $path | ConvertFrom-Json).vaultToken | Should -Be 'hvs.xyz'
    }

    It 'creates the parent directory when it is missing' {
        $path = Join-Path $TestDrive 'nested/dir/secrets.json'

        Write-DaprToken -Token 'hvs.xyz' -Path $path

        Test-Path $path | Should -BeTrue
    }

    It 'does not write a file in -WhatIf mode' {
        $path = Join-Path $TestDrive 'whatif.json'

        Write-DaprToken -Token 'hvs.xyz' -Path $path -WhatIf

        Test-Path $path | Should -BeFalse
    }
}

Describe 'Invoke-Main' {
    It 'performs a full first-run initialization' {
        Mock Invoke-VaultApi {
            switch ($Path) {
                'v1/sys/seal-status'     { return [pscustomobject]@{ sealed = $true } }
                'v1/sys/init'            {
                    if ($Method -eq 'Put') {
                        return [pscustomobject]@{ keys_base64 = @('unseal-key'); root_token = 'hvs.root' }
                    }
                    return [pscustomobject]@{ initialized = $false }
                }
                'v1/sys/unseal'          { return [pscustomobject]@{ sealed = $false } }
                'v1/sys/mounts/secret'   { return $null }
                'v1/secret/data/mongodb' { return [pscustomobject]@{} }
                default                  { throw "unexpected API call: $Method $Path" }
            }
        }
        $DaprSecretsPath = Join-Path $TestDrive 'firstrun.json'

        Invoke-Main

        (Get-Content -Raw $DaprSecretsPath | ConvertFrom-Json).vaultToken | Should -Be 'hvs.root'
    }

    It 'unseals and refreshes the DAPR token when Vault is already initialized' {
        Mock Invoke-VaultApi {
            switch ($Path) {
                'v1/sys/seal-status' { return [pscustomobject]@{ sealed = $true } }
                'v1/sys/init'        { return [pscustomobject]@{ initialized = $true } }
                'v1/sys/unseal'      { return [pscustomobject]@{ sealed = $false } }
                default              { throw "unexpected API call: $Method $Path" }
            }
        }
        $UnsealKey       = 'unseal-key'
        $RootToken       = 'hvs.root'
        $DaprSecretsPath = Join-Path $TestDrive 'restart.json'

        Invoke-Main

        (Get-Content -Raw $DaprSecretsPath | ConvertFrom-Json).vaultToken | Should -Be 'hvs.root'
    }

    It 'throws when Vault is initialized but no unseal key is configured' {
        Mock Invoke-VaultApi {
            switch ($Path) {
                'v1/sys/seal-status' { return [pscustomobject]@{ sealed = $true } }
                'v1/sys/init'        { return [pscustomobject]@{ initialized = $true } }
                default              { throw "unexpected API call: $Method $Path" }
            }
        }
        $UnsealKey = ''

        { Invoke-Main } | Should -Throw '*VAULT_UNSEAL_KEY*'
    }

    It 'previews a first run with -WhatIf without calling any state-changing endpoint' {
        Mock Invoke-VaultApi {
            if ($Method -eq 'Get' -and $Path -eq 'v1/sys/init') {
                return [pscustomobject]@{ initialized = $false }
            }
            throw "state-changing API call must not happen under WhatIf: $Method $Path"
        }
        $DaprSecretsPath = Join-Path $TestDrive 'whatif-main.json'

        { Invoke-Main -WhatIf } | Should -Not -Throw

        Test-Path $DaprSecretsPath | Should -BeFalse
    }
}
