"""Health and system-info endpoints."""

from fastapi import APIRouter, Request

from sdapp_backend.inference.samplers import available_samplers

router = APIRouter(tags=["system"])


@router.get("/health")
def health(request: Request) -> dict[str, object]:
    pipeline_manager = request.app.state.pipeline_manager
    return {
        "status": "ok",
        "device": pipeline_manager.device,
        "loaded_model": pipeline_manager.model_id,
    }


@router.get("/samplers")
def samplers() -> list[str]:
    return available_samplers()
