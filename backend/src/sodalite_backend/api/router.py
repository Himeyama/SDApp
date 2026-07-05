"""Aggregates all API sub-routers under a single prefix."""

from fastapi import APIRouter

from sodalite_backend.api import generation, system

api_router = APIRouter(prefix="/api/v1")
api_router.include_router(system.router)
api_router.include_router(generation.router)
