"""Embeds Sodalite's own generation-parameter metadata into PNG files."""

import json
from collections.abc import Mapping
from pathlib import Path

from PIL import Image
from PIL.PngImagePlugin import PngInfo

METADATA_KEY = "sodalite_metadata"


def save_with_metadata(image: Image.Image, path: Path, parameters: Mapping[str, object]) -> None:
    """Save `image` as PNG at `path`, embedding `parameters` as a JSON tEXt chunk."""
    png_info = PngInfo()
    png_info.add_text(METADATA_KEY, json.dumps(parameters, ensure_ascii=False))
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, pnginfo=png_info)


def read_metadata(path: Path) -> dict[str, object] | None:
    """Read back the Sodalite metadata JSON embedded by `save_with_metadata`, if present."""
    with Image.open(path) as image:
        raw = image.text.get(METADATA_KEY) if hasattr(image, "text") else None
    return json.loads(raw) if raw else None
