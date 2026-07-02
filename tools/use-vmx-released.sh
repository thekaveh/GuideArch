#!/usr/bin/env bash
# Switch the repo back to its committed VMx consumption mode.
# Run from repo root. Pin the Python PyPI version with VMX_VERSION=… (default below).
#
# NOTE: Python is the released default in the committed tree (vmx is pulled from
# PyPI by langs/python/pyproject.toml), so this script's Python branch only
# re-asserts that state — it's mainly use-vmx-local.sh's inverse. VMx 3.1.0 is
# not published to npm or NuGet yet, so TypeScript and C# remain on the local
# vendor/vmx submodule in the committed default.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VMX_VERSION="${VMX_VERSION:-3.1.0}"

echo "→ TypeScript: keeping VMx on vendor/vmx/langs/typescript (npm package not published)"
node -e 'const p=require("./langs/typescript/package.json"); p.dependencies=p.dependencies||{}; p.dependencies.vmx="file:../../vendor/vmx/langs/typescript"; require("fs").writeFileSync("./langs/typescript/package.json", JSON.stringify(p,null,2)+"\n")'
echo "  ok"

echo "→ C#: keeping VMxSource=local (NuGet package not published) and recording VMxVersion=${VMX_VERSION}"
sed -i.bak -E "s|(<VMxSource Condition=[^>]+>)[^<]*(</VMxSource>)|\1local\2|" langs/csharp/Directory.Build.props
sed -i.bak -E "s|<VMxVersion>[^<]*</VMxVersion>|<VMxVersion>${VMX_VERSION}</VMxVersion>|" langs/csharp/Directory.Build.props
rm langs/csharp/Directory.Build.props.bak
echo "  ok"

echo "→ Python: pointing langs/python at PyPI vmx==${VMX_VERSION}"
# Comment the [tool.uv.sources] editable entry out (use-vmx-local.sh restores
# it) and re-lock so the toggle persists across syncs. This is the committed
# default, so the sed is a no-op unless use-vmx-local.sh ran first; a bare
# `uv pip install` would not suffice because the next `uv sync` reinstalls
# whatever [tool.uv.sources] pins.
sed -i.bak -E 's|^vmx = \{ path = |# vmx = { path = |' langs/python/pyproject.toml
rm langs/python/pyproject.toml.bak
( cd langs/python && uv lock --upgrade-package "vmx==${VMX_VERSION}" && uv sync --all-extras )
echo "  ok"

echo "Done. Python consumes VMx ${VMX_VERSION} from PyPI; TypeScript and C# use vendor/vmx."
