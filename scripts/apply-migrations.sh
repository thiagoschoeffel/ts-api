#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${ConnectionStrings__Database:-}" ]]; then
  echo "ConnectionStrings__Database precisa apontar para o banco do ambiente." >&2
  exit 2
fi

dotnet tool restore
dotnet restore
dotnet build src/Ts.Api.Api/Ts.Api.Api.csproj --configuration Release --no-restore --disable-build-servers -m:1
dotnet tool run dotnet-ef database update \
  --project src/Ts.Api.Infrastructure \
  --startup-project src/Ts.Api.Api \
  --no-build \
  --configuration Release
