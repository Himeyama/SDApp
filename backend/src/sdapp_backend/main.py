"""FastAPI application entry point for the SDApp backend."""

from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI
from fastapi.staticfiles import StaticFiles

from sdapp_backend.api.router import api_router
from sdapp_backend.config import load_config
from sdapp_backend.imaging.storage import OUTPUT_DIR
from sdapp_backend.inference.pipeline_manager import PipelineManager


def create_app(model_id: str) -> FastAPI:
    @asynccontextmanager
    async def lifespan(app: FastAPI):
        app.state.pipeline_manager = PipelineManager(model_id)
        yield

    app = FastAPI(title="SDApp Backend", lifespan=lifespan)
    app.include_router(api_router)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    app.mount("/api/v1/images", StaticFiles(directory=OUTPUT_DIR), name="images")

    return app


def run() -> None:
    config = load_config()
    app = create_app(config.model_id)
    uvicorn.run(app, host=config.host, port=config.port)


if __name__ == "__main__":
    run()
