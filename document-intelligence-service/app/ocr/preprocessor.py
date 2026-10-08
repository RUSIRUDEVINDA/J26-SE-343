"""Deterministic OpenCV image preprocessing pipeline for OCR enhancement."""

from typing import Union
import cv2
import numpy as np
from PIL import Image


class ImagePreprocessor:
    """Modular, deterministic image preprocessor using OpenCV."""

    @staticmethod
    def to_numpy(image: Union[Image.Image, np.ndarray]) -> np.ndarray:
        """Convert a PIL Image or bytes array to a numpy BGR/grayscale array."""
        if isinstance(image, Image.Image):
            # Convert PIL Image to RGB numpy array, then to BGR for OpenCV
            rgb_array = np.array(image.convert("RGB"))
            return cv2.cvtColor(rgb_array, cv2.COLOR_RGB2BGR)
        elif isinstance(image, np.ndarray):
            return image.copy()
        else:
            raise ValueError(f"Unsupported image type: {type(image)}")

    @staticmethod
    def to_grayscale(image: np.ndarray) -> np.ndarray:
        """Convert BGR image to grayscale. If already single-channel, returns as-is."""
        if len(image.shape) == 2:
            return image
        if len(image.shape) == 3 and image.shape[2] == 1:
            return image[:, :, 0]
        return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

    @staticmethod
    def normalize_contrast(gray: np.ndarray) -> np.ndarray:
        """Enhance local contrast using Contrast Limited Adaptive Histogram Equalization (CLAHE)."""
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        return clahe.apply(gray)

    @staticmethod
    def denoise(gray: np.ndarray) -> np.ndarray:
        """Reduce high-frequency salt-and-pepper noise while preserving stroke edges."""
        # Median blur with 3x3 kernel is lightweight and avoids blurring small Sinhala diacritics
        return cv2.medianBlur(gray, 3)

    @staticmethod
    def threshold_otsu(gray: np.ndarray) -> np.ndarray:
        """Binarize using Otsu's optimal global thresholding."""
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        return binary

    @staticmethod
    def deskew(binary_or_gray: np.ndarray) -> np.ndarray:
        """Detect and correct document rotation skew angle (-45 to +45 degrees)."""
        # Invert colors so text is foreground (white) on black background for contour calculation
        if len(binary_or_gray.shape) > 2:
            gray = cv2.cvtColor(binary_or_gray, cv2.COLOR_BGR2GRAY)
        else:
            gray = binary_or_gray

        _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
        coords = np.column_stack(np.where(thresh > 0))

        if coords.size == 0:
            return binary_or_gray

        # minAreaRect returns ((center_x, center_y), (width, height), angle)
        angle = cv2.minAreaRect(coords)[-1]

        # Determine skew angle correction
        if angle < -45:
            angle = -(90 + angle)
        elif angle > 45:
            angle = 90 - angle
        else:
            angle = -angle

        # If angle is negligible (< 0.5 degrees), do not rotate to avoid resampling artifacts
        if abs(angle) < 0.5 or abs(angle) > 45.0:
            return binary_or_gray

        (h, w) = binary_or_gray.shape[:2]
        center = (w // 2, h // 2)
        rotation_matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
        deskewed = cv2.warpAffine(
            binary_or_gray,
            rotation_matrix,
            (w, h),
            flags=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_CONSTANT,
            borderValue=255,  # White background fill
        )
        return deskewed

    @classmethod
    def preprocess(cls, image: Union[Image.Image, np.ndarray]) -> np.ndarray:
        """
        Execute deterministic full preprocessing pipeline:
        Grayscale -> Contrast Normalization -> Denoising -> Otsu Binarization -> Deskew.
        """
        img_np = cls.to_numpy(image)
        gray = cls.to_grayscale(img_np)
        normalized = cls.normalize_contrast(gray)
        denoised = cls.denoise(normalized)
        binary = cls.threshold_otsu(denoised)
        deskewed = cls.deskew(binary)
        return deskewed
