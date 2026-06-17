# Tags are mutable; pin by multi-arch manifest digest for reproducible builds.
# Dependabot's docker ecosystem keeps digest pins fresh automatically.
FROM python:3.14-slim@sha256:d7a925f9eb9639a93e455b9f12c167569358818c0f62b51b88edbc8fcf34c421

# Install uv. Pin to a real version tag — `:latest` made the image build
# non-reproducible (a uv release could silently change CLI behavior) — and
# additionally by digest, same rationale as the base image above.
COPY --from=ghcr.io/astral-sh/uv:0.8@sha256:1d31be550ff927957472b2a491dc3de1ea9b5c2d319a9cea5b6a48021e2990a6 /uv /usr/local/bin/uv

WORKDIR /app/langs/python

# vmx is resolved from PyPI (see langs/python/pyproject.toml), so the build
# context no longer needs the vendor/vmx submodule — build with langs/python
# as the context:  docker build -f langs/python/Dockerfile langs/python
#
# The README is referenced by pyproject's `readme = "README.md"` and hatchling
# fails the build if it isn't present when `uv sync` runs, so it must travel
# with the project files.
COPY pyproject.toml uv.lock README.md ./
# src/ includes guidearch/samples/*.json (bundled sample scenarios) and the
# committed schema copy at guidearch/_data/scenario.schema.json, so the
# schema travels with the package — no need to COPY spec/ at runtime.
COPY src ./src

# Sync production dependencies only (no dev extras)
RUN uv sync --no-dev

EXPOSE 8080

CMD ["uv", "run", "guidearch", "--port", "8080"]
