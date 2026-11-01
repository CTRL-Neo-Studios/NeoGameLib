#!/usr/bin/env bash
# rebuilds Inter.xnb from Inter.spritefont with the mgcb version pinned in .config/dotnet-tools.json (the dotnet-mgcb package)
set -euo pipefail
cd "$(dirname "$0")"
mgcb=$(ls -d ~/.nuget/packages/dotnet-mgcb/*/tools/net*/any | tail -1)
dotnet "$mgcb/mgcb.dll" /@:Builtin.mgcb
cp bin/Inter.xnb Inter.xnb
rm -rf bin obj
