"""GuideArch — Python implementation."""

from importlib.metadata import PackageNotFoundError
from importlib.metadata import version as _pkg_version

try:
    # Read from the installed distribution metadata so __version__ always
    # matches pyproject.toml's [project].version (currently 1.0.0). Hardcoding
    # would drift; we caught it once at "0.0.0".
    __version__ = _pkg_version("guidearch")
except PackageNotFoundError:
    # Editable / source checkout where the distribution isn't formally
    # installed. Fall back to a sentinel that's obviously not a release.
    __version__ = "0.0.0+unknown"
