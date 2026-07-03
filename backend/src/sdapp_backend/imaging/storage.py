"""Output directory management for generated images."""

import uuid
from pathlib import Path

OUTPUT_DIR = Path("outputs")


def new_image_path() -> Path:
    return OUTPUT_DIR / f"{uuid.uuid4().hex}.png"
