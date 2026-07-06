"""Request/response schemas for the generation API."""

from typing import Literal

from pydantic import BaseModel, Field

Sampler = Literal["euler_a", "euler", "dpmpp_2m", "ddim", "lms"]

JobStatus = Literal["queued", "running", "completed", "failed", "cancelled"]


class LoraSpec(BaseModel):
    """A LoRA to apply during generation, identified the same way as a base model.

    `model_id` is either a Hugging Face repo id or an absolute path to a
    single-file `.safetensors` LoRA. `weight` scales its influence; negative
    values invert the effect.
    """

    model_id: str
    weight: float = Field(default=1.0, ge=-2.0, le=2.0)


class TextToImageRequest(BaseModel):
    prompt: str
    negative_prompt: str = ""
    steps: int = Field(default=20, ge=1, le=150)
    cfg_scale: float = Field(default=7.0, ge=0.0, le=30.0)
    width: int = Field(default=512, ge=64, le=2048, multiple_of=8)
    height: int = Field(default=512, ge=64, le=2048, multiple_of=8)
    sampler: Sampler = "euler_a"
    seed: int | None = None
    loras: list[LoraSpec] = Field(default_factory=list)


class GalleryLoraInfo(BaseModel):
    model_id: str
    weight: float


class GalleryParameters(BaseModel):
    """Generation parameters read back from a PNG's embedded metadata.

    Deliberately has no Field constraints (unlike TextToImageRequest): this
    describes historical data that was already valid when generated, and must
    keep loading even if future request validation ranges change.
    """

    prompt: str = ""
    negative_prompt: str = ""
    steps: int | None = None
    cfg_scale: float | None = None
    width: int | None = None
    height: int | None = None
    sampler: str | None = None
    seed: int | None = None
    loras: list[GalleryLoraInfo] = Field(default_factory=list)


class GalleryImageInfo(BaseModel):
    """A generated image found on disk, with its embedded generation parameters if present.

    `parameters` is null when the PNG carries no Sodalite metadata (e.g. a file
    placed into the output directory manually, or a build predating metadata
    embedding).
    """

    image_id: str
    image_url: str
    image_path: str
    created_at: float
    parameters: GalleryParameters | None = None


class GenerationJob(BaseModel):
    job_id: str
    status: JobStatus
    progress: float = 0.0
    current_step: int = 0
    total_steps: int = 0
    image_url: str | None = None
    image_path: str | None = None
    error: str | None = None


class ModelInfo(BaseModel):
    model_id: str
    is_active: bool
    size_on_disk_bytes: int


class SetActiveModelRequest(BaseModel):
    model_id: str


class LoraFileInfo(BaseModel):
    """A LoRA file available locally, identified by its absolute path."""

    lora_id: str
    size_on_disk_bytes: int


class ScanDirectoriesInfo(BaseModel):
    """The user-configured directories scanned for checkpoints and LoRA files.

    Either may be null when unset; the corresponding scan then yields nothing.
    """

    model_dir: str | None = None
    lora_dir: str | None = None
