#!/bin/sh

export VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"

# Returns 0 if Vault is reachable (sealed or unsealed), 1 if not yet up.
test_vault_running() {
  vault status > /dev/null 2>&1
  rc=$?
  [ $rc -eq 0 ] || [ $rc -eq 2 ]
}

test_vault_initialized() {
  response=$(wget -qO- "${VAULT_ADDR}/v1/sys/init" 2>/dev/null || echo '{"initialized":false}')
  initialized=$(
    echo "$response" \
    | grep -Eo '"initialized":(true|false)' \
    | grep -Eo 'true|false'
  )
  [ "$initialized" = "true" ]
}

initialize_vault() {
  if ! INIT_OUTPUT=$(vault operator init -key-shares=1 -key-threshold=1); then
    echo "ERROR: vault operator init failed with result:"
    echo "$INIT_OUTPUT"
    exit 1
  fi

  UNSEAL_KEY=$(echo "$INIT_OUTPUT" | grep "Unseal Key 1" | awk '{print $NF}')
  ROOT_TOKEN=$(echo "$INIT_OUTPUT" | grep "Initial Root Token" | awk '{print $NF}')

  if [ -z "$UNSEAL_KEY" ] || [ -z "$ROOT_TOKEN" ]; then
    echo "ERROR: Failed to parse Vault init output:"
    echo "$INIT_OUTPUT"
    exit 1
  fi

  VAULT_TOKEN="$ROOT_TOKEN"
  export VAULT_TOKEN
}

unseal_vault() {
  vault operator unseal "$1"
}

seed_secrets() {
  vault secrets enable -path=secret kv-v2
  vault kv put secret/mongodb \
    username="${MONGO_ROOT_USER:-changeme}" \
    password="${MONGO_ROOT_PASSWORD:-changeme}"
}

write_dapr_token() {
  printf '{\n  "vaultToken": "%s"\n}\n' "$1" > /dapr/secrets.json
}

print_init_summary() {
  echo ""
  echo "============================================================"
  echo "  VAULT INITIALIZED"
  echo "  Add these to your .env file, then restart:"
  echo ""
  printf '  VAULT_UNSEAL_KEY=%s\n' "$1"
  printf '  VAULT_ROOT_TOKEN=%s\n' "$2"
  echo ""
  echo "  Verify secrets at http://localhost:8200"
  echo "============================================================"
  echo ""
}

# ── main ────────────────────────────────────────────────────────────────────

until test_vault_running; do
  echo "Waiting for Vault..."
  sleep 2
done
echo "Vault is up."

if ! test_vault_initialized; then
  echo "=== First run: initializing Vault ==="
  initialize_vault
  unseal_vault "$UNSEAL_KEY"
  seed_secrets
  write_dapr_token "$ROOT_TOKEN"
  print_init_summary "$UNSEAL_KEY" "$ROOT_TOKEN"
else
  if [ -z "$VAULT_UNSEAL_KEY" ]; then
    echo "ERROR: Vault is initialized but VAULT_UNSEAL_KEY is not set in .env"
    exit 1
  fi
  unseal_vault "$VAULT_UNSEAL_KEY"
  VAULT_TOKEN="$VAULT_ROOT_TOKEN"
  export VAULT_TOKEN
  write_dapr_token "$VAULT_ROOT_TOKEN"
  echo "Vault unsealed."
fi
