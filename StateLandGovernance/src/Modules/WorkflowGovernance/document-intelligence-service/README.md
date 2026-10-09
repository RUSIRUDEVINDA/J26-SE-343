# Document Intelligence Service (OCR-1)
## State Land Lease Governance — Component 3 External Microservice

This microservice provides an isolated, deterministic OCR extraction pipeline for the State Land Lease Governance platform. It operates strictly outside the .NET Application and Domain layers, delivering machine-generated observations without legal or case authority.

---

## 1. Scope & Capabilities

- **Engine**: Tesseract OCR + OpenCV preprocessing.
- **Languages Supported**:
  - English (`eng`)
  - Sinhala (`sin`)
  - Mixed Sinhala + English (`sin+eng`)
- **Input Formats**:
  - PDF (`application/pdf`) via PyMuPDF rasterization (300 DPI default)
  - PNG (`image/png`)
  - JPEG (`image/jpeg`)
- **Provenance**: Page-level OCR results with 1-based page indexing, character counts, and token-level OCR confidence.
- **Preprocessing Pipeline**:
  - Grayscale conversion
  - CLAHE contrast normalization
  - Median denoising (preserving Sinhala diacritics)
  - Otsu binarization
  - Deskew rotation correction (-45° to +45°)
- **Evaluation Utility**: Levenshtein-based CER (Character Error Rate) and WER (Word Error Rate) with conservative Unicode NFC normalization.

---

## 2. Prerequisites

1. **Python**: Python 3.10+ (tested on Python 3.13)
2. **Tesseract OCR**:
   - **Windows**:
     - Download and run the Windows installer (e.g., from [UB-Mannheim/tesseract](https://github.com/UB-Mannheim/tesseract/wiki)).
     - Ensure the **Sinhala** language pack (`sin.traineddata`) is selected during installation or downloaded into the `tessdata` directory.
     - Add the installation folder to system `PATH` or configure `TESSERACT_CMD` in `.env`.
   - **Linux (Ubuntu/Debian)**:
     ```bash
     sudo apt-get update
     sudo apt-get install -y tesseract-ocr tesseract-ocr-sin tesseract-ocr-eng
     ```

---

## 3. Environment Setup & Installation

### A. Clone & Navigate
```bash
cd document-intelligence-service
```

### B. Configure Environment
Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```

If Tesseract is not on your system `PATH`, configure `TESSERACT_CMD`:
```ini
# Windows Example
TESSERACT_CMD=C:\Program Files\Tesseract-OCR\tesseract.exe

# Linux Example
TESSERACT_CMD=/usr/bin/tesseract

OCR_DEFAULT_LANGUAGE=sin+eng
OCR_MAX_UPLOAD_MB=25
OCR_PDF_DPI=300
LOG_LEVEL=INFO
```

### C. Install Dependencies
```bash
pip install -e .
```
Or install directly:
```bash
pip install -r pyproject.toml
```

---

## 4. Running the Service

Start the FastAPI HTTP service using Uvicorn:
```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

Interactive OpenAPI documentation is available at:
- Swagger UI: `http://localhost:8000/docs`
- ReDoc: `http://localhost:8000/redoc`

---

## 5. API Usage

### A. Health Check & Engine Diagnostic
```bash
curl -X GET http://localhost:8000/health
```

Example response (Healthy):
```json
{
  "status": "healthy",
  "tesseract_available": true,
  "tesseract_version": "5.3.4",
  "available_languages": ["eng", "osd", "sin"]
}
```

Example response (Degraded / Tesseract missing):
```json
{
  "status": "degraded",
  "tesseract_available": false,
  "tesseract_version": null,
  "available_languages": []
}
```

### B. Extract OCR from Document
```bash
curl -X POST "http://localhost:8000/v1/ocr?language_mode=sin+eng&preprocess=true" \
  -H "X-Request-ID: req-lease-case-001" \
  -F "file=@/path/to/deed_or_plan.pdf"
```

Example response:
```json
{
  "request_id": "req-lease-case-001",
  "language_mode": "sin+eng",
  "preprocessed": true,
  "page_count": 2,
  "pages": [
    {
      "page_number": 1,
      "raw_text": "රාජ්‍ය ඉඩම් බදු ගිවිසුම - STATE LAND LEASE AGREEMENT\nඅංකය: LA-2026-901\n",
      "clean_text": "රාජ්‍ය ඉඩම් බදු ගිවිසුම - STATE LAND LEASE AGREEMENT\nඅංකය: LA-2026-901",
      "text": "රාජ්‍ය ඉඩම් බදු ගිවිසුම - STATE LAND LEASE AGREEMENT\nඅංකය: LA-2026-901",
      "character_count": 71,
      "confidence": 92.4
    },
    {
      "page_number": 2,
      "raw_text": "පිඹුරු අංකය: SP-2026-004\nප්‍රමාණය: අක්කර 2 රූඩ් 1\n",
      "clean_text": "පිඹුරු අංකය: SP-2026-004\nප්‍රමාණය: අක්කර 2 රූඩ් 1",
      "text": "පිඹුරු අංකය: SP-2026-004\nප්‍රමාණය: අක්කර 2 රූඩ් 1",
      "character_count": 51,
      "confidence": 88.7
    }
  ],
  "total_character_count": 122,
  "duration_ms": 380.25
}
```

---

## 6. Running Automated Tests

Run unit tests via `pytest`:
```bash
python -m pytest
```

All 53 automated unit tests mock Tesseract execution where appropriate and do not require external runtime dependencies to pass on CI/CD runners.

---

## 7. OCR Evaluation Utility (CER / WER)

Compute Character Error Rate (CER) and Word Error Rate (WER) against ground truth transcriptions:

```bash
# Using positional file paths:
python -m app.evaluation.metrics ground_truth.txt ocr_output.txt

# Using explicit file flags:
python -m app.evaluation.metrics --reference-file ground_truth.txt --hypothesis-file ocr_output.txt

# Using inline text strings:
python -m app.evaluation.metrics --reference-text "ශ්‍රී ලංකා රජය" --hypothesis-text "ශ්‍රී ලංකා රජය"
```

Example output:
```json
{
  "raw": {
    "cer": 0.0,
    "wer": 0.0
  },
  "normalized": {
    "cer": 0.0,
    "wer": 0.0
  },
  "counts": {
    "reference_characters": 13,
    "hypothesis_characters": 13,
    "reference_words": 3,
    "hypothesis_words": 3
  }
}
```

---

## 8. Architectural Boundaries & Non-Authority Disclaimer

- This service produces **unverified machine observations only**.
- It does **not** create Domain facts, approve proposals, perform regulatory screening, or mutate case workflow state.
- Integration with Component 3 is handled via `IDocumentIntelligenceService` through a decoupled Infrastructure HTTP adapter in a future checkpoint.
