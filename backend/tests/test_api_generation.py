"""Tests for the generation and system API endpoints."""

from fastapi.testclient import TestClient


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
