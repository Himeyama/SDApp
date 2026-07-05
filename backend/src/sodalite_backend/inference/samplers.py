"""Mapping between sampler names exposed by the API and diffusers scheduler classes."""

from diffusers import (
    DDIMScheduler,
    DPMSolverMultistepScheduler,
    EulerAncestralDiscreteScheduler,
    EulerDiscreteScheduler,
    LMSDiscreteScheduler,
    SchedulerMixin,
)

from sodalite_backend.schemas.generation import Sampler

SAMPLER_CLASSES: dict[Sampler, type[SchedulerMixin]] = {
    "euler_a": EulerAncestralDiscreteScheduler,
    "euler": EulerDiscreteScheduler,
    "dpmpp_2m": DPMSolverMultistepScheduler,
    "ddim": DDIMScheduler,
    "lms": LMSDiscreteScheduler,
}


def available_samplers() -> list[Sampler]:
    return list(SAMPLER_CLASSES.keys())
