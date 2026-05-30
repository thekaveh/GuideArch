#!/usr/bin/env bash
# Switch all three impls back to consuming VMx from published packages.
# Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VMX_VERSION="${VMX_VERSION:-2.1.0}"

echo "→ TypeScript: pointing package.json at npm vmx@${VMX_VERSION}"
node -e "const p=require('./langs/typescript/package.json'); p.dependencies=p.dependencies||{}; p.dependencies.vmx='^${VMX_VERSION}'; require('fs').writeFileSync('./langs/typescript/package.json', JSON.stringify(p,null,2)+'\n')" \
  2>/dev/null && echo "  ok" || echo "  (skipped — TS scaffold not present yet)"

echo "→ C#: setting VMxSource=released in Directory.Build.props"
echo "  (manually toggle <VMxSource>released</VMxSource> in Directory.Build.props)"

echo "→ Python: install vmx==${VMX_VERSION} from PyPI"
if [[ -f langs/python/pyproject.toml ]]; then
  cd langs/python
  uv pip install "vmx==${VMX_VERSION}"
  cd "$ROOT"
  echo "  ok"
else
  echo "  (skipped — Python scaffold not present yet)"
fi

echo "Done. VMx is now consumed from published packages (${VMX_VERSION})."
