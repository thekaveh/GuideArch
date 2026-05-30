#!/usr/bin/env bash
# Switch all three impls to consume VMx from the local submodule (editable).
# Run from repo root.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "→ TypeScript: pointing langs/typescript/package.json at vendor/vmx/langs/typescript"
node -e 'const p=require("./langs/typescript/package.json"); p.dependencies=p.dependencies||{}; p.dependencies.vmx="file:../../vendor/vmx/langs/typescript"; require("fs").writeFileSync("./langs/typescript/package.json", JSON.stringify(p,null,2)+"\n")'
echo "  ok"

echo "→ C#: setting VMxSource=local in langs/csharp/Directory.Build.props"
# The <VMxSource Condition="..."> default is what we mutate. Match the
# opening tag with a literal string so we don't touch unrelated content.
sed -i.bak -E 's|(<VMxSource Condition=[^>]+>)[^<]*(</VMxSource>)|\1local\2|' langs/csharp/Directory.Build.props
rm langs/csharp/Directory.Build.props.bak
echo "  ok"

echo "→ Python: editable install from vendor/vmx/langs/python"
( cd langs/python && uv pip install -e ../../vendor/vmx/langs/python )
echo "  ok"

echo "Done. VMx is now consumed locally from vendor/vmx/."
