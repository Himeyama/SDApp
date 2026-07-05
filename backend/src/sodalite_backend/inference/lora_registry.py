"""Lists LoRA files found by scanning the user-configured LoRA directory."""

from pathlib import Path

from sodalite_backend.inference.directories_store import load_directories
from sodalite_backend.schemas.generation import LoraFileInfo

LORA_EXTENSIONS = {".safetensors"}


def list_imported_loras() -> list[LoraFileInfo]:
    """List LoRA files under the configured LoRA directory, sorted by path."""
    lora_dir = load_directories().lora_dir
    if lora_dir is None:
        return []

    return [
        LoraFileInfo(lora_id=str(path), size_on_disk_bytes=path.stat().st_size)
        for path in scan_lora_files(Path(lora_dir))
    ]


def scan_lora_files(directory: Path) -> list[Path]:
    """Recursively find supported LoRA files under directory, sorted by path."""
    if not directory.is_dir():
        return []

    files = [
        path
        for path in directory.rglob("*")
        if path.is_file() and path.suffix.lower() in LORA_EXTENSIONS
    ]
    return sorted(files)
