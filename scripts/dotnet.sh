#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

docker run --rm \
  --volume "${repo_dir}:/src" \
  --workdir /src \
  --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  --env NUGET_PACKAGES=/src/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet "$@"
