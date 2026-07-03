"""Shared fixtures for the API test suite."""

from collections.abc import Iterator
from unittest.mock import MagicMock

import pytest
from fastapi.testclient import TestClient
from PIL import Image


@pytest.fixture
def mock_pipeline_manager() -> MagicMock:
    manager = MagicMock()
    manager.device = "cpu"
    manager.model_id = "stub/model"
    manager.generate.return_value = Image.new("RGB", (8, 8))
    return manager


@pytest.fixture
def client(mock_pipeline_manager: MagicMock, tmp_path, monkeypatch) -> Iterator[TestClient]:
    monkeypatch.chdir(tmp_path)

    from sdapp_backend import main as main_module

    monkeypatch.setattr(main_module, "PipelineManager", lambda model_id: mock_pipeline_manager)

    app = main_module.create_app("stub/model")
    with TestClient(app) as test_client:
        yield test_client
