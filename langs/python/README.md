# GuideArch — Python

Python + NiceGUI 3.x implementation of GuideArch.

The UI renders the shared two-theme design system (`spec/design-system.md`) via NiceGUI/Tailwind CSS variables — dark default + light, toggled from the toolbar (results charts re-render on toggle).

## Prerequisites

- Python 3.11+ (`python3 --version`)
- [uv](https://github.com/astral-sh/uv) (`uv --version`; install via `curl -LsSf https://astral.sh/uv/install.sh | sh`)

VMx is pulled from PyPI ([`vmx`](https://pypi.org/project/vmx/)) — `uv sync` resolves it automatically, so the `vendor/vmx/` submodule is **not** required for the Python impl. (Initialize it only to co-develop VMx via `tools/use-vmx-local.sh`.)

## Dev setup

```bash
cd langs/python
uv sync --all-extras                # unit-test, lint, type-check tooling
uv sync --all-extras --group visual # also install playwright for visual snapshots
```

## Run

```bash
uv run guidearch                          # web mode — browser at http://localhost:8080
uv run guidearch --native                 # desktop — native pywebview window
uv run python -m guidearch.main --native  # equivalent module-form invocation
uv run guidearch --port 9000              # override port
```

The console-script form (`uv run guidearch --native`) and the module form (`uv run python -m guidearch.main --native`) are equivalent: the entry point auto re-execs into the module form so NiceGUI's multiprocessing spawn child can re-import a stable, package-qualified `__main__`. Without that, on some platforms the HTTP server starts (`NiceGUI ready to go on http://127.0.0.1:8080`) but the pywebview window never opens.

After the app loads, click **Sample SAS** or **Sample EDS** in the toolbar (or the **Open Sample SAS** CTA on the first-launch hero) to try a bundled scenario; or use **Open…** to pick your own JSON file.

## Test

```bash
uv run pytest tests/ -q                              # full unit + integration suite
uv run python -m guidearch.conformance.runner        # cross-impl numerical conformance
uv run ruff check src tests                          # lint
uv run mypy src tests                                # type-check (strict)
```

The integration suite under `tests/integration/` and the VM-MVVM tests under `tests/unit/test_vm_mvvm.py` exercise the ScenarioVM and its children **without mounting any NiceGUI components** — that's the MVVM separation in action.

## Docker

The `Dockerfile` at `langs/python/Dockerfile` builds the web UI into a self-contained image. Because VMx now installs from PyPI, the build context is just `langs/python/` (no longer the repo root / `vendor/vmx/`):

```bash
# Build (context = langs/python)
docker build -f langs/python/Dockerfile -t guidearch:latest langs/python

# Run — web UI at http://localhost:8080
docker run --rm -p 8080:8080 guidearch:latest
```

The published image is at `ghcr.io/thekaveh/guidearch:1.0.0` (when the release workflow has the GHCR token configured).
