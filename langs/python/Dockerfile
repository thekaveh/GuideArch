FROM python:3.12-slim

# Install uv
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv

WORKDIR /app

# Copy the vmx vendor submodule (Python language port) needed for editable install
COPY vendor/vmx/langs/python /app/vendor/vmx/langs/python

# Copy the GuideArch Python project. The README is referenced by pyproject's
# `readme = "README.md"` and hatchling fails the build if it isn't present
# when `uv sync` runs, so it must travel with the project files.
COPY langs/python/pyproject.toml langs/python/uv.lock langs/python/README.md /app/langs/python/
# src/ includes guidearch/samples/*.json (bundled sample scenarios)
COPY langs/python/src /app/langs/python/src
COPY spec/conformance /app/spec/conformance
COPY spec/domain /app/spec/domain

# Sync production dependencies only (no dev extras)
WORKDIR /app/langs/python
RUN uv sync --no-dev

EXPOSE 8080

CMD ["uv", "run", "guidearch", "--port", "8080"]
