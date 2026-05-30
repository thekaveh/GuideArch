# GuideArch — Python

Python + NiceGUI 3.x implementation of GuideArch.

## Dev setup

```bash
cd langs/python
uv sync
```

## Run

```bash
uv run guidearch          # web (browser, default port 8080)
uv run guidearch --native # desktop (pywebview window)
```

## Docker

The `Dockerfile` at `langs/python/Dockerfile` builds the web UI into a self-contained image. Run from the **repo root** (the Docker context must include `vendor/vmx/`):

```bash
# Build
docker build -f langs/python/Dockerfile -t guidearch:latest .

# Run — web UI at http://localhost:8080
docker run --rm -p 8080:8080 guidearch:latest
```

The published image is at `ghcr.io/thekaveh/guidearch:1.0.0`.
