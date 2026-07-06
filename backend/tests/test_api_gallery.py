"""Tests for the gallery API endpoints."""

from pathlib import Path

from fastapi.testclient import TestClient
from PIL import Image


def _generate_one(client: TestClient, prompt: str = "a cat") -> dict:
    response = client.post("/api/v1/generations/text-to-image", json={"prompt": prompt, "steps": 4})
    assert response.status_code == 200
    return response.json()


def test_list_images_empty_when_no_outputs(client: TestClient) -> None:
    response = client.get("/api/v1/gallery/images")
    assert response.status_code == 200
    assert response.json() == []


def test_list_images_returns_generated_image_with_parameters(client: TestClient) -> None:
    job = _generate_one(client, prompt="a cat wearing sunglasses")

    response = client.get("/api/v1/gallery/images")

    assert response.status_code == 200
    body = response.json()
    assert len(body) == 1
    assert body[0]["image_url"] == job["image_url"]
    assert body[0]["parameters"]["prompt"] == "a cat wearing sunglasses"


def test_list_images_sorted_newest_first(client: TestClient) -> None:
    _generate_one(client, prompt="first")
    _generate_one(client, prompt="second")

    body = client.get("/api/v1/gallery/images").json()

    assert [image["parameters"]["prompt"] for image in body] == ["second", "first"]


def test_list_images_skips_corrupt_file(client: TestClient, tmp_path: Path) -> None:
    (tmp_path / "outputs").mkdir(exist_ok=True)
    (tmp_path / "outputs" / "broken.png").write_bytes(b"not a real png")

    response = client.get("/api/v1/gallery/images")

    assert response.status_code == 200
    assert response.json() == []


def test_list_images_includes_file_without_metadata(client: TestClient, tmp_path: Path) -> None:
    outputs = tmp_path / "outputs"
    outputs.mkdir(exist_ok=True)
    Image.new("RGB", (4, 4)).save(outputs / "no_metadata.png")

    body = client.get("/api/v1/gallery/images").json()

    assert len(body) == 1
    assert body[0]["parameters"] is None


def test_delete_image_removes_file(client: TestClient, tmp_path: Path) -> None:
    job = _generate_one(client)
    image_id = job["image_url"].removeprefix("/api/v1/images/")

    response = client.delete(f"/api/v1/gallery/images/{image_id}")

    assert response.status_code == 204
    assert not (tmp_path / "outputs" / image_id).exists()


def test_delete_image_missing_returns_404(client: TestClient) -> None:
    response = client.delete("/api/v1/gallery/images/does-not-exist.png")
    assert response.status_code == 404


def test_delete_image_rejects_path_traversal(client: TestClient, tmp_path: Path) -> None:
    outside = tmp_path / "secret.png"
    Image.new("RGB", (4, 4)).save(outside)

    response = client.delete("/api/v1/gallery/images/..%2Fsecret.png")

    assert response.status_code == 404
    assert outside.exists()
