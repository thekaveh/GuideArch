FROM python:3.12-slim

# Install uv
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv

WORKDIR /app

# Copy the vmx vendor submodule (Python language port) needed for editable install
COPY vendor/vmx/langs/python /app/vendor/vmx/langs/python

# Copy the GuideArch Python project
COPY langs/python/pyproject.toml langs/python/uv.lock /app/langs/python/
COPY langs/python/src /app/langs/python/src
COPY spec/conformance /app/spec/conformance
COPY spec/domain /app/spec/domain

# Sync production dependencies only (no dev extras)
WORKDIR /app/langs/python
RUN uv sync --no-dev

EXPOSE 8080

CMD ["uv", "run", "guidearch", "--port", "8080"]
