"""Tests for the generation and system API endpoints."""

from fastapi.testclient import TestClient

from sdapp_backend.schemas.generation import ModelInfo


def test_health(client: TestClient) -> None:
    response = client.get("/api/v1/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok", "device": "cpu", "loaded_model": "stub/model"}


def test_samplers(client: TestClient) -> None:
    response = client.get("/api/v1/samplers")
    assert response.status_code == 200
    assert "euler_a" in response.json()


def test_text_to_image_returns_completed_job(client: TestClient) -> None:
    response = client.post(
        "/api/v1/generations/text-to-image",
        json={"prompt": "a cat wearing sunglasses", "steps": 4},
    )
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "completed"
    assert body["image_url"].startswith("/api/v1/images/")


def test_text_to_image_rejects_invalid_dimensions(client: TestClient) -> None:
    response = client.post(
        "/api/v1/generations/text-to-image",
        json={"prompt": "test", "width": 10},
    )
    assert response.status_code == 422


def test_list_models(client: TestClient, monkeypatch) -> None:
    fake_models = [
        ModelInfo(model_id="stub/model", is_active=True, size_on_disk_bytes=123),
        ModelInfo(model_id="other/model", is_active=False, size_on_disk_bytes=456),
    ]
    monkeypatch.setattr(
        "sdapp_backend.api.system.list_cached_models", lambda active_model_id: fake_models
    )

    response = client.get("/api/v1/models")

    assert response.status_code == 200
    body = response.json()
    assert body == [model.model_dump() for model in fake_models]


def test_set_active_model(client: TestClient, mock_pipeline_manager) -> None:
    def load_model(model_id: str) -> None:
        mock_pipeline_manager.model_id = model_id

    mock_pipeline_manager.load_model.side_effect = load_model

    response = client.post("/api/v1/models/active", json={"model_id": "other/model"})

    assert response.status_code == 200
    assert response.json() == {
        "model_id": "other/model",
        "is_active": True,
        "size_on_disk_bytes": 0,
    }
    mock_pipeline_manager.load_model.assert_called_once_with("other/model")


def test_set_active_model_rejects_unknown_model(
    client: TestClient, mock_pipeline_manager
) -> None:
    mock_pipeline_manager.load_model.side_effect = OSError("model not found")

    response = client.post("/api/v1/models/active", json={"model_id": "no/such-model"})

    assert response.status_code == 422
