# GuideArch

GuideArch helps software architects compare technology stacks using fuzzy multi-criteria decision analysis.
Describe decisions, alternatives, quality properties and constraints, then rank candidate architectures with fuzzy TOPSIS.
Use sensitivity analysis to see which decisions and constraints affect the result.

## 1. Capabilities

- Edit architecture decisions, alternatives, property weights and constraints.
- Rank candidates and inspect scores, fuzzy values and comparison charts.
- Examine critical decisions and constraints.
- Load bundled examples, open scenario JSON files and save your changes.
- Choose dark or light appearance in any implementation.

## 2. Availability and requirements

Run GuideArch from source using the instructions below.
[GitHub Releases](https://github.com/thekaveh/GuideArch/releases) currently contains only `v0.0.0-bootstrap`, with no attached installers.
The `v1.0.0` tag and package metadata identify the development baseline; they do not establish downloadable release availability.
Release build configurations are documented in [the release specification](spec/release.md).

Choose one implementation. You do not need all three toolchains.

| Implementation | Requirements | Interface |
| --- | --- | --- |
| [Python](langs/python/README.md) | Git, Python 3.11+, uv | NiceGUI browser UI; optional pywebview desktop window |
| [TypeScript](langs/typescript/README.md) | Git, Node 22+, pnpm 11+; Rust/cargo for desktop | Svelte browser UI or Tauri desktop |
| [C#](langs/csharp/README.md) | Git, .NET 8 or 9 SDK | Avalonia desktop; WebAssembly support is planned |

Python installs VMx from its pinned PyPI dependency.
TypeScript and C# build VMx from the `vendor/vmx/` submodule because their registry packages are not published.
The language guides explain platform-specific desktop prerequisites and optional VMx co-development.

## 3. Run from source

Install the tools for your chosen implementation first.
Each block below starts from the repository root of a fresh checkout.

```bash
git clone https://github.com/thekaveh/GuideArch.git
cd GuideArch
```

### 3.1. Python browser application

```bash
cd langs/python
uv sync
uv run guidearch
```

Open `http://localhost:8080` in your browser.
Use `uv run guidearch --native` for a desktop window when your platform supports pywebview.
Stop the server with `Ctrl+C`.
See [Python setup](langs/python/README.md) for native-mode details and local development checks.

### 3.2. TypeScript application

From the repository root:

```bash
git submodule update --init vendor/vmx
cd langs/typescript
pnpm install
pnpm dev
```

Open `http://localhost:1420` in your browser.
Use `pnpm tauri dev` for the desktop application after installing its native prerequisites.
Stop the development server with `Ctrl+C`.
See [TypeScript setup](langs/typescript/README.md) for builds and tests.

### 3.3. C# application

From the repository root:

```bash
git submodule update --init vendor/vmx
cd langs/csharp
dotnet build
dotnet run --project src/GuideArch.View
```

An Avalonia desktop window opens. Close it to stop the application.
See [C# setup](langs/csharp/README.md) for framework, runtime and test details.

## 4. Explore a sample

1. Select **Sample SAS** in the toolbar, or **Open Sample SAS** on the initial screen.
2. Open **Results** to inspect the ranked candidates. The sample's top score is `0.031180695179944085`.
3. Open **Properties** and change a weight, such as *Reliability*. The rankings update after the change.
4. Inspect **Critical Decisions** and **Critical Constraints** to understand the result.
5. Use **Save As…** to save a temporary JSON file. Select **New**, then **Open…** that file to restore your changes.

The bundled SAS scenario contains 10 decisions, 25 alternatives and seven properties.
**Sample EDS** provides a second example.
The [scenario corpus](spec/conformance/scenarios/) and [algorithm specification](spec/algorithms/topsis.md) explain the inputs and expected results.

## 5. Development and reference

Start with [Contributing](CONTRIBUTING.md) for the spec-first workflow and local verification.
Each language guide contains its unit, integration and numerical conformance commands.
Python development tools use `uv sync --all-extras`; `uv sync` installs the runtime dependencies only.

| Reference | Purpose |
| --- | --- |
| [Documentation reference](docs/reference.md) | Specifications, design decisions, release process and archived audit evidence |
| [Changelog](CHANGELOG.md) | Development milestones and recorded changes |
| [Contract ledger](docs/contract-ledger.md) | Consumed dependencies and toolchain contracts |
| [Security](SECURITY.md) | Private vulnerability reporting |
| [Code of conduct](CODE_OF_CONDUCT.md) | Participation policy |

The repository separates shared specifications (`spec/`), language implementations (`langs/`) and supporting scripts (`tools/`).
VMx is an external submodule under `vendor/vmx/`; propose dependency changes upstream.

## 6. License

MIT — see [LICENSE](LICENSE).
