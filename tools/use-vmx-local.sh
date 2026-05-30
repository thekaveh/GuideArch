#!/usr/bin/env bash
# Switch all three impls to consume VMx from the local submodule (editable).
# Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "→ TypeScript: pointing package.json at vendor/vmx/langs/typescript"
# To be wired in M0 Task 18 (TS scaffold). For now, just print.
node -e 'const p=require("./langs/typescript/package.json"); p.dependencies=p.dependencies||{}; p.dependencies.vmx="file:../../vendor/vmx/langs/typescript"; require("fs").writeFileSync("./langs/typescript/package.json", JSON.stringify(p,null,2)+"\n")' \
  2>/dev/null && echo "  ok" || echo "  (skipped — TS scaffold not present yet)"

echo "→ C#: pointing ViewModels.csproj at vendor/vmx/langs/csharp/src/VMx/VMx.csproj"
# Handled by Directory.Build.props variable in M0 Task 22.
echo "  (set VMxSource=local in Directory.Build.props)"

echo "→ Python: editable install from vendor/vmx/langs/python"
if [[ -f langs/python/pyproject.toml ]]; then
  cd langs/python
  uv pip install -e ../../vendor/vmx/langs/python
  cd "$ROOT"
  echo "  ok"
else
  echo "  (skipped — Python scaffold not present yet)"
fi

echo "Done. VMx is now consumed locally from vendor/vmx/."
