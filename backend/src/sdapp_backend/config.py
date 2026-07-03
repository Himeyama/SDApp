"""Runtime configuration for the backend server."""

import argparse
import os
from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class AppConfig:
    host: str
    port: int
    model_id: str


DEFAULT_MODEL_ID = "stabilityai/sd-turbo"


def load_config() -> AppConfig:
    """Build config from CLI args, falling back to the SDAPP_PORT env var."""
    parser = argparse.ArgumentParser(prog="sdapp-backend")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=int(os.environ.get("SDAPP_PORT", "8000")))
    parser.add_argument("--model-id", default=DEFAULT_MODEL_ID)
    args = parser.parse_args()
    return AppConfig(host=args.host, port=args.port, model_id=args.model_id)
