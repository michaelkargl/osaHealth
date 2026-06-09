#!/bin/bash
set -e
source /workspaces/serena/.venv/bin/activate
dotnet restore /workspace/osaHealth/src/osaHealth.Api/osaHealth.Api.fsproj
exec "$@"