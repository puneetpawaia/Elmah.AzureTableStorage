#!/usr/bin/env bash
set -euo pipefail
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_HOME="$repository_root/.tools/dotnet-home"
export NUGET_PACKAGES="$repository_root/.nuget/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$repository_root/.tools/bin:$PATH"
if [[ -x "$repository_root/.tools/dotnet/dotnet" ]]; then
    exec "$repository_root/.tools/dotnet/dotnet" "$@"
fi
exec dotnet "$@"
