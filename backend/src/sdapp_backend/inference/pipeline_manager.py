"""Loads and holds the active diffusers pipeline for inference."""

import torch
from diffusers import AutoPipelineForText2Image

from sdapp_backend.inference.samplers import SAMPLER_CLASSES
from sdapp_backend.schemas.generation import Sampler


class PipelineManager:
    """Owns a single loaded text-to-image pipeline, moved onto the best available device."""

    def __init__(self, model_id: str) -> None:
        self.model_id = model_id
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        dtype = torch.float16 if self.device == "cuda" else torch.float32
        self._pipeline = AutoPipelineForText2Image.from_pretrained(model_id, torch_dtype=dtype).to(
            self.device
        )

    def set_sampler(self, sampler: Sampler) -> None:
        scheduler_cls = SAMPLER_CLASSES[sampler]
        self._pipeline.scheduler = scheduler_cls.from_config(self._pipeline.scheduler.config)

    def generate(
        self,
        prompt: str,
        negative_prompt: str,
        steps: int,
        cfg_scale: float,
        width: int,
        height: int,
        sampler: Sampler,
        seed: int | None,
    ):
        self.set_sampler(sampler)
        generator = None
        if seed is not None:
            generator = torch.Generator(device=self.device).manual_seed(seed)

        result = self._pipeline(
            prompt=prompt,
            negative_prompt=negative_prompt or None,
            num_inference_steps=steps,
            guidance_scale=cfg_scale,
            width=width,
            height=height,
            generator=generator,
        )
        return result.images[0]
