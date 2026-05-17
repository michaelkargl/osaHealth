# osaHealth

A simple yet powerful health tracking service for your family.

## Installation

1. Install [pwsh](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell)
1. Install [Node.js](https://nodejs.org/)
1. ```pwsh
   npm install
   npm run husky:init
   npm run ai:init
   # Use the claude-mem runtime: Server

   # Copy and fill in your credentials
   cp .env.example .env
   code .env
   ```

## First-run setup

The stack requires a one-time Vault initialization before all services are fully operational.

### 1. Configure environment variables

Fill in the MongoDB and mongo-express credentials in `.env` (copied from `.env.example`).  
Leave `VAULT_UNSEAL_KEY` and `VAULT_ROOT_TOKEN` blank for now.

### 2. Start the stack

```pwsh
docker compose up -d
```

`vault-init` will detect an uninitialized Vault, generate an unseal key and root token, and print them to its logs.

### 3. Save the Vault credentials

```pwsh
docker compose logs vault-init
```

Look for the block starting with `VAULT INITIALIZED`. Copy the two values into `.env`:

```
VAULT_UNSEAL_KEY=<value from logs>
VAULT_ROOT_TOKEN=<value from logs>
```

Restart so the unseal key takes effect:

```pwsh
docker compose up -d
```

### 4. Restart

```pwsh
docker compose up -d
```

The stack is now fully operational. `vault-init` seeds MongoDB credentials directly from `.env` and unseals Vault automatically on every subsequent restart.

You can verify or manage all secrets at **http://localhost:8200** using `VAULT_ROOT_TOKEN`.

### Resetting Vault (start over)

If you need to wipe Vault and reinitialize from scratch (e.g., lost the unseal key):

```pwsh
docker compose down
docker volume rm osahealth_vault_data
# Clear VAULT_UNSEAL_KEY and VAULT_ROOT_TOKEN from .env
docker compose up -d
# Then follow steps 3–4 above to capture new keys and fill in secrets
```

---

## Development

### Database

Access the database via Mongo Express: <http://127.0.0.1:8081>
- See [docker-compose.yml] for port details
- See [.env] for credentials

### Vault UI

Manage application secrets at <http://localhost:8200>
- Sign in with `VAULT_ROOT_TOKEN` from `.env`

### Vault initialization

The `vault-init` container runs [`vault/init/init.ps1`](./vault/init/init.ps1), a PowerShell
script that initializes/unseals Vault over its HTTP API. It needs neither the `vault`
CLI nor a POSIX shell, so the team can read and maintain it like the rest of the codebase.

Preview what an invocation would do without touching Vault:

```pwsh
pwsh -File ./vault/init/init.ps1 -WhatIf
```

The script is covered by Pester tests ([`vault/init/init.Tests.ps1`](./vault/init/init.Tests.ps1)):

```pwsh
Invoke-Pester -Path ./vault/init -Output Detailed
```

[docker-compose.yml]: ./docker-compose.yml
[.env]: ./.env
