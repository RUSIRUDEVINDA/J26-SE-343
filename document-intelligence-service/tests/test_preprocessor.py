"""Unit tests for OpenCV image preprocessing pipeline."""

import numpy as np
import pytest
from PIL import Image
from app.ocr.preprocessor import ImagePreprocessor


def test_to_numpy_from_pil():
    img = Image.new("RGB", (100, 50), color=(255, 0, 0))
    arr = ImagePreprocessor.to_numpy(img)
    assert isinstance(arr, np.ndarray)
    assert arr.shape == (50, 100, 3)
    # BGR format in OpenCV: red is (0, 0, 255)
    assert arr[0, 0, 0] == 0
    assert arr[0, 0, 2] == 255


def test_to_numpy_from_ndarray():
    orig = np.zeros((40, 60, 3), dtype=np.uint8)
    arr = ImagePreprocessor.to_numpy(orig)
    assert isinstance(arr, np.ndarray)
    assert arr.shape == (40, 60, 3)


def test_to_numpy_invalid_type():
    with pytest.raises(ValueError, match="Unsupported image type"):
        ImagePreprocessor.to_numpy("not-an-image")


def test_to_grayscale_from_bgr():
    bgr = np.ones((50, 80, 3), dtype=np.uint8) * 120
    gray = ImagePreprocessor.to_grayscale(bgr)
    assert len(gray.shape) == 2
    assert gray.shape == (50, 80)


def test_to_grayscale_already_grayscale():
    orig_gray = np.ones((50, 80), dtype=np.uint8) * 200
    gray = ImagePreprocessor.to_grayscale(orig_gray)
    assert gray.shape == (50, 80)
    assert np.array_equal(orig_gray, gray)


def test_normalize_contrast_preserves_dimensions():
    gray = np.arange(256, dtype=np.uint8).reshape((16, 16))
    norm = ImagePreprocessor.normalize_contrast(gray)
    assert norm.shape == (16, 16)
    assert norm.dtype == np.uint8


def test_denoise_preserves_dimensions():
    gray = np.random.randint(0, 256, (100, 100), dtype=np.uint8)
    denoised = ImagePreprocessor.denoise(gray)
    assert denoised.shape == (100, 100)
    assert denoised.dtype == np.uint8


def test_threshold_otsu_produces_binary():
    gray = np.zeros((50, 50), dtype=np.uint8)
    gray[20:30, 20:30] = 255
    binary = ImagePreprocessor.threshold_otsu(gray)
    unique_vals = set(np.unique(binary))
    assert unique_vals.issubset({0, 255})


def test_deskew_straight_image():
    # Straight horizontal box
    gray = np.ones((200, 200), dtype=np.uint8) * 255
    gray[90:110, 50:150] = 0
    deskewed = ImagePreprocessor.deskew(gray)
    assert deskewed.shape == (200, 200)


def test_preprocess_pipeline_complete():
    pil_img = Image.new("RGB", (200, 100), color=(240, 240, 240))
    result = ImagePreprocessor.preprocess(pil_img)
    assert isinstance(result, np.ndarray)
    assert len(result.shape) == 2
    assert result.shape == (100, 200)
    assert result.dtype == np.uint8
    unique_vals = set(np.unique(result))
    assert unique_vals.issubset({0, 255})
