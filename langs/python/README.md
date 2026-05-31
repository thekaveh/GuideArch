# GuideArch — Python

Python + NiceGUI 3.x implementation of GuideArch.

## Prerequisites

- Python 3.11+ (`python3 --version`)
- [uv](https://github.com/astral-sh/uv) (`uv --version`; install via `curl -LsSf https://astral.sh/uv/install.sh | sh`)
- The VMx submodule initialised at `vendor/vmx/` (run `git submodule update --init` from the repo root if not)

## Dev setup

```bash
cd langs/python
uv sync --all-extras                # unit-test, lint, type-check tooling
uv sync --all-extras --group visual # also install playwright for visual snapshots
```

## Run

```bash
uv run guidearch                # web mode — browser at http://localhost:8080
uv run guidearch --native       # desktop — native pywebview window
uv run guidearch --port 9000    # override port
```

After the app loads, click **Open Sample SAS** or **Open Sample EDS** in the toolbar to try a bundled scenario; or use **Open…** to pick your own JSON file.

## Test

```bash
uv run pytest tests/ -q                              # full unit + integration suite
uv run python -m guidearch.conformance.runner        # cross-impl numerical conformance
uv run ruff check src tests                          # lint
uv run mypy src tests                                # type-check (strict)
```

The integration suite under `tests/integration/` and the VM-MVVM tests under `tests/unit/test_vm_mvvm.py` exercise the ScenarioVM and its children **without mounting any NiceGUI components** — that's the MVVM separation in action.

## Docker

The `Dockerfile` at `langs/python/Dockerfile` builds the web UI into a self-contained image. Run from the **repo root** (the Docker context must include `vendor/vmx/`):

```bash
# Build
docker build -f langs/python/Dockerfile -t guidearch:latest .

# Run — web UI at http://localhost:8080
docker run --rm -p 8080:8080 guidearch:latest
```

The published image is at `ghcr.io/thekaveh/guidearch:1.0.0` (when the release workflow has the GHCR token configured).
