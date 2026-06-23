# GuideArch — C#

C# (.NET 8) + Avalonia 12 implementation of GuideArch.

The UI renders the shared two-theme design system (`spec/design-system.md`) via Avalonia `DynamicResource` theme brushes — dark default + light, toggled from the toolbar (charts retint on toggle).

## Prerequisites

- .NET 8 SDK or newer (`dotnet --list-sdks`). .NET 9 SDK works because `Directory.Build.props` sets `<RollForward>Major</RollForward>` — the published binary targets `net8.0` but rolls forward at runtime to whatever the user has.
- The VMx submodule initialised at `vendor/vmx/` (run `git submodule update --init` from the repo root if not).

## Run

```bash
cd langs/csharp
dotnet build GuideArch.sln
dotnet run --project src/GuideArch.View   # opens a native Avalonia window
```

After the window opens, click **Sample SAS** or **Sample EDS** in the toolbar (or the **Open Sample SAS** CTA on the first-launch hero) to try a bundled scenario; the OS-native file picker is used for **Open…**.

## WebAssembly (Avalonia.Browser) — deferred to v1.1

The C# View project does not yet reference `Avalonia.Browser`, so
`dotnet publish -r browser-wasm` will fail at runtime. v1.0 ships C# as
desktop-only, matching `spec/release.md` §1.2. When the wasm target lands,
this section will document the publish command and the static-host setup.

## Test

```bash
dotnet test --nologo                                    # all unit + integration tests
dotnet run --project src/GuideArch.Conformance          # cross-impl numerical conformance
dotnet format GuideArch.sln                             # auto-format
dotnet format GuideArch.sln --verify-no-changes         # CI gate
```

The integration suite at `tests/GuideArch.ViewModels.Tests/VMMvvmIntegrationTests.cs` exercises the ViewModel factory and `ScenarioState` **without instantiating any Avalonia controls** — that's the MVVM separation in action.

## Visual snapshots

The `tools/screenshot-all-tabs/` console tool runs the full Avalonia app under `Avalonia.Headless` (no real window system, dispatcher on a dedicated thread) and writes per-tab PNGs to `tests/visual/snapshots/`. Useful for visual diffs after a UI change.

```bash
dotnet run --project tools/screenshot-all-tabs   # writes PNGs to tests/visual/snapshots/
```

Mirrors the Python visual harness at `langs/python/tests/visual/snapshot_all_tabs.py` and the TS one at `langs/typescript/tests/visual/snapshot-all-tabs.mjs`. Not wired into CI; intended for manual cross-impl review.

## Solution layout

- `src/GuideArch.Models/` — domain entities + TOPSIS pipeline (`Topsis/Solver.cs`, `CriticalDecisions.cs`, `CriticalConstraints.cs`) + `ScenarioLoader.cs` + JSON output
- `src/GuideArch.ViewModels/` — `ScenarioVMFactory.cs` (factory pattern because `ComponentVM<M>` is sealed), child VM factories, `ScenarioMutator`, `SampleScenarios.cs`
- `src/GuideArch.View/` — Avalonia 12 desktop app (`MainWindow.axaml` is the 8-tab editor + analysis UI)
- `src/GuideArch.Conformance/` — console runner that validates outputs against `spec/conformance/expected/`
- `tools/screenshot-all-tabs/` — headless-Avalonia per-tab PNG capture (see *Visual snapshots* above)
- `tests/GuideArch.Models.Tests/` — unit tests for Models + Topsis
- `tests/GuideArch.ViewModels.Tests/` — unit tests for VMs + MVVM integration

## Architecture notes

- VMx-C# `ComponentVM<M>` is `sealed`, so VMs are realised as **static-factory functions** returning configured `ComponentVM<M>`. See ADR-0001 §"VMx improvements flow back upstream" for the rationale: upstream-VMx changes (e.g. unsealing the class) happen via PR against the `vendor/vmx` submodule, not by patching the build here.
- Sample scenarios ship as **embedded resources** under `src/GuideArch.View/Assets/Samples/`; `SampleScenarios.Open(sample)` returns a `Stream` that `MainWindow.axaml.cs` writes to a temp file and feeds to `ScenarioLoader.Load(path)`.
- Every binding into the VM in `MainWindow.axaml` uses `{ReflectionBinding}` because the DataContext type (`ComponentVM<ScenarioState>`) is open-generic and cannot be expressed as a XAML `x:DataType` — see the comment block at the top of `MainWindow.axaml` for the longer rationale. A non-generic wrapper VM type would let us switch to compiled `{Binding}`, but that refactor is not on the v1.x roadmap; the ReflectionBinding count is intentional and load-bearing.
