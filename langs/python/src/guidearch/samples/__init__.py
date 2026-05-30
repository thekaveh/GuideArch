"""Bundled sample scenarios shipped with the package."""

from pathlib import Path

_SAMPLES_DIR = Path(__file__).parent

SAMPLES = [
    {
        "id": "sas",
        "label": "SAS — Service-Oriented Architecture",
        "path": _SAMPLES_DIR / "sas.json",
    },
    {
        "id": "eds",
        "label": "EDS — Enterprise Decision Space",
        "path": _SAMPLES_DIR / "eds.json",
    },
]
