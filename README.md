# GuideArch

Modern fuzzy multi-criteria decision analysis for software architecture.

GuideArch helps software architects model a decision space — *decisions*, *alternatives*, quality *properties*, and *constraints* — and ranks the resulting candidate architectures using fuzzy TOPSIS. It identifies which decisions matter most (sensitivity analysis) and which constraints are most binding (elimination counting).

Three implementations of the same application share one language-neutral spec:

| Language | UI Framework | Desktop | Web |
|---|---|---|---|
| TypeScript | Svelte 5 + [Tauri 2](https://tauri.app) | ✓ | ✓ |
| C# | [Avalonia 12](https://avaloniaui.net) | ✓ | ✓ (WebAssembly) |
| Python | [NiceGUI 3.x](https://nicegui.io) | ✓ (pywebview) | ✓ |

All three are built on the [VMx](https://github.com/thekaveh/VMx) MVVM framework, included as a git submodule at `vendor/vmx/`.

## Status

Bootstrap (M0). Not yet usable. See [docs/design/](docs/design/) for the rewrite spec and [docs/plans/](docs/plans/) for the milestone plans.

## Repository layout

```
spec/                 language-neutral spec (schema, algorithms, conformance corpus, ADRs)
vendor/vmx/           VMx submodule
langs/typescript/     TS + Tauri + Svelte impl
langs/csharp/         C# + Avalonia impl
langs/python/         Python + NiceGUI impl
tools/                cross-cutting scripts
docs/                 design specs and milestone plans
```

## Quickstart (per impl)

Bootstrap stage — each impl runs a hello-world that imports VMx as a wiring proof.

### TypeScript

```bash
cd langs/typescript
pnpm install
pnpm dev          # browser
pnpm tauri dev    # desktop
```

### C#

```bash
cd langs/csharp
dotnet build
dotnet run --project src/GuideArch.View
```

### Python

```bash
cd langs/python
uv sync
uv run guidearch          # web
uv run guidearch --native # desktop (pywebview)
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All implementations must stay in conformance with `spec/conformance/`.

## License

MIT — see [LICENSE](LICENSE).
