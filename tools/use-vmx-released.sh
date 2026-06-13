#!/usr/bin/env bash
# Switch all three impls back to consuming VMx from published packages.
# Run from repo root. Pin the version with VMX_VERSION=… (defaults below).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VMX_VERSION="${VMX_VERSION:-2.1.0}"

echo "→ TypeScript: pointing langs/typescript/package.json at npm vmx@^${VMX_VERSION}"
node -e "const p=require('./langs/typescript/package.json'); p.dependencies=p.dependencies||{}; p.dependencies.vmx='^${VMX_VERSION}'; require('fs').writeFileSync('./langs/typescript/package.json', JSON.stringify(p,null,2)+'\n')"
echo "  ok"

echo "→ C#: setting VMxSource=released and VMxVersion=${VMX_VERSION} in langs/csharp/Directory.Build.props"
sed -i.bak -E "s|(<VMxSource Condition=[^>]+>)[^<]*(</VMxSource>)|\1released\2|" langs/csharp/Directory.Build.props
sed -i.bak -E "s|<VMxVersion>[^<]*</VMxVersion>|<VMxVersion>${VMX_VERSION}</VMxVersion>|" langs/csharp/Directory.Build.props
rm langs/csharp/Directory.Build.props.bak
echo "  ok"

echo "→ Python: pointing langs/python at PyPI vmx==${VMX_VERSION}"
# A bare `uv pip install` only mutates the venv: the next `uv sync`/`uv run`
# would reinstall the editable submodule pinned by [tool.uv.sources]. Comment
# the source entry out (use-vmx-local.sh restores it) and re-lock so the
# toggle persists across syncs.
sed -i.bak -E 's|^vmx = \{ path = |# vmx = { path = |' langs/python/pyproject.toml
rm langs/python/pyproject.toml.bak
( cd langs/python && uv lock --upgrade-package "vmx==${VMX_VERSION}" && uv sync --all-extras )
echo "  ok"

echo "Done. VMx is now consumed from published packages (${VMX_VERSION})."
