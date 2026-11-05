#!/usr/bin/env bash
# Установка .NET SDK и восстановление/сборка решения для Cloud Agent.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$REPO_ROOT/FrontolConfigurator-Agent.sln"

DOTNET_CHANNEL="10.0"
DOTNET_ROOT_DIR="/usr/local/dotnet"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Ставим .NET 10 SDK только если его ещё нет — install идемпотентен.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
    tmp_dir="$(mktemp -d)"
    curl -Lsfo "$tmp_dir/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
    chmod +x "$tmp_dir/dotnet-install.sh"
    sudo mkdir -p "$DOTNET_ROOT_DIR"
    sudo "$tmp_dir/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT_DIR" --no-path
    sudo ln -sf "$DOTNET_ROOT_DIR/dotnet" /usr/local/bin/dotnet
    rm -rf "$tmp_dir"
fi

dotnet --info

dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" --no-restore -c Debug
