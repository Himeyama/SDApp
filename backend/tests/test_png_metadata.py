"""Tests for PNG metadata embedding."""

import json

from PIL import Image

from sodalite_backend.imaging.png_metadata import (
    PARAMETERS_KEY,
    format_parameters_text,
    read_metadata,
    save_with_metadata,
)


def test_save_with_metadata_embeds_json_and_parameters_text(tmp_path) -> None:
    image = Image.new("RGB", (8, 8))
    path = tmp_path / "out.png"
    parameters = {
        "prompt": "a cat wearing sunglasses",
        "negative_prompt": "blurry",
        "steps": 20,
        "cfg_scale": 7.0,
        "width": 512,
        "height": 512,
        "sampler": "euler_a",
        "seed": 42,
        "loras": [{"model_id": "some/lora", "weight": 0.8}],
    }

    save_with_metadata(image, path, parameters)

    assert read_metadata(path) == parameters

    with Image.open(path) as reopened:
        parameters_text = reopened.text[PARAMETERS_KEY]

    assert parameters_text == (
        "a cat wearing sunglasses\n"
        "Negative prompt: blurry\n"
        "Steps: 20, Sampler: euler_a, CFG scale: 7.0, Seed: 42, Size: 512x512, "
        "LoRAs: some/lora:0.8"
    )


def test_format_parameters_text_omits_missing_fields() -> None:
    text = format_parameters_text({"prompt": "a cat"})

    assert text == "a cat\nNegative prompt: \n"


def test_format_parameters_text_round_trips_through_json_like_values() -> None:
    parameters = json.loads(
        json.dumps({"prompt": "p", "negative_prompt": "n", "steps": 10, "seed": None})
    )

    text = format_parameters_text(parameters)

    assert "Steps: 10" in text
    assert "Seed:" not in text
