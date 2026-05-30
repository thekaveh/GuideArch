"""
One-shot importer: legacy GuideArch XML scenarios → JSON.

This script is a stub at M0. Full implementation is part of M1, when the JSON
schema in `spec/domain/scenario.schema.json` is finalized.

Usage (M1+):
    python tools/import-legacy-xml.py <path/to/legacy.xml> -o spec/conformance/scenarios/<name>.json
"""
from __future__ import annotations

import sys


def main() -> int:
    print("tools/import-legacy-xml.py: not yet implemented (M1 work).", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
