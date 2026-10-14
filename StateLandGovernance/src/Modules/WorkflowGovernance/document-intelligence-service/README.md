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
cd StateLandGovernance/src/Modules/WorkflowGovernance/document-intelligence-service
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

## 8. Ollama Real Model Integration (QWEN-1B)

In addition to pure OCR extraction (`/v1/ocr`), this service provides a governed semantic extraction pipeline via `POST /v1/analyze` backed by local Ollama HTTP inference.

### A. Ollama Setup & Development Model
1. **Install Ollama**:
   Download and install from [ollama.ai](https://ollama.ai).
2. **Pull the Development Model**:
   ```bash
   ollama pull qwen3:1.7b
   ```
3. **Verify Model Installation**:
   ```bash
   ollama list
   ```
   Confirm `qwen3:1.7b` appears in the list with valid digest.
4. **Default URL & Daemon**:
   Ollama serves locally at `http://localhost:11434` without authentication.
   Ensure the Ollama service or desktop app is running before invoking live semantic extraction.
5. **Development Model Disclaimer**:
   > `qwen3:1.7b` is the current **DEVELOPMENT** model for local development and validation.
   > The final evaluation model may be frozen later after validation comparison.
   > 1.7B is chosen for local responsiveness and low memory footprint; no claim is made that 1.7B is the best or optimal model for high-stakes production.

### B. Core Architecture & Non-Authority Boundary
- **Candidate-Only Output**: Machine outputs produced by Qwen/NLP models are strictly candidate facts (`ExtractedCandidateFact`).
- **No Automatic Authority**: The model never makes legal, routing, screening, approval, rejection, or completion decisions.
- **Thinking Disabled**: Every request to Ollama explicitly specifies `"think": false`. Hidden reasoning traces are never generated or stored.
- **Human Verification Flow**: Machine candidate facts flow to the .NET application, where authorized human officers must review and confirm (`Confirmed`), correct (`Corrected`), or reject (`Unsupported`). Only human-verified facts enter the immutable `VerifiedFactSnapshot`.
- **Backward Compatibility**: The `/v1/ocr` endpoint remains 100% backward compatible and unchanged.

### C. Composite Pipeline (`POST /v1/analyze`)
The composite analysis endpoint executes:
1. **Document Ingestion & Preprocessing**: PDF rasterization and image contrast enhancement.
2. **OCR Extraction**: Tesseract OCR producing page-indexed transcripts and confidence scores.
3. **Deterministic Chunking**: Text grouped into page-preserving chunks within token budget limits.
4. **Prompt Injection Defense**: Untrusted OCR text wrapped in strict `<UNTRUSTED_DOCUMENT_PAGES>` delimiters with versioned extraction instructions (`qwen-proposal-extraction-v2`).
5. **Ollama Structured Inference**: Chat completion against `POST /api/chat` with `model="qwen3:1.7b"`, `think=false`, `temperature=0.0`, and schema-constrained structured output.
6. **Deterministic Grounding Verification**: Every candidate fact must quote text that verbatim appears on the referenced page. Character offsets (`start_offset`, `end_offset`) are computed against clean page text. Fabricated or hallucinated quotes fail verification (`is_verified=false`) and generate warnings.
7. **JSON Validation & Schema Repair**: Output validated against canonical schemas, bounding values, and clamping confidence to `[0.0, 1.0]`.

### D. Configuration
Configured via `.env` or environment variables:
```ini
SEMANTIC_PROVIDER=ollama          # "ollama" (local Ollama HTTP daemon) or "stub" (deterministic regex)
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=qwen3:1.7b
OLLAMA_TIMEOUT_SECONDS=60.0
OLLAMA_THINK=false
OLLAMA_TEMPERATURE=0.0
OLLAMA_KEEP_ALIVE=5m
SEMANTIC_PROMPT_VERSION=qwen-proposal-extraction-v2
RUN_QWEN_LIVE_TESTS=false         # Gate for live Ollama inference in test suite
```

### E. Composite Analysis Request Example
```bash
curl -X POST "http://localhost:8000/v1/analyze?language_mode=sin+eng&preprocess=true" \
  -H "X-Request-ID: req-semantic-001" \
  -F "file=@/path/to/proposal_deed.pdf"
```

---

## 9. Semantic Evaluation Framework & Benchmark Protocol

The service includes an evaluation suite for measuring semantic extraction accuracy, classification Macro-F1, evidence-span precision, and reproducibility:

### A. Evaluation Manifest Schema (`dataset_manifest_template.json`)
The manifest standardizes:
- **80 Target Documents**: 40 development, 16 validation, 24 held-out test documents.
- **Document Categories**: Project proposals, lease applications, survey plans, title deeds, valuation reports.
- **Language Coverage**: Sinhala-dominant, English-dominant, and mixed bilingual.
- **45 Manual OCR Ground-Truth Pages**: For paired CER/WER and semantic evaluation.
- All uncollected documents are explicitly marked `PLANNED`.

### B. Evaluation Runner
Execute evaluation across an annotated dataset manifest:
```bash
python -m app.evaluation.runner \
  --manifest evaluation/dataset_manifest_template.json \
  --split development \
  --output-dir evaluation/results
```

Outputs:
- JSON evaluation report with:
  - Classification Macro-F1, precision, recall
  - Field-level precision, recall, and F1
  - Critical-field accuracy (`ApplicantName`, `NicNumber`, `ExtentAcres`, `StatutoryBasis`)
  - Grounded evidence-span precision (exact match & verification rate)
  - Character Error Rate (CER) and Word Error Rate (WER)
- Environment reproducibility metadata: Ollama version, model digest (`/api/tags`), commit hash, hardware device, model ID, prompt version, temperature, timestamp.
- Per-document extraction breakdown CSV.

---

## 10. Architectural Boundaries & Non-Authority Disclaimer

- **Candidate Facts Only**: Qwen produces unverified candidate observations only.
- **Non-Authoritative Classification**: Machine document classification is non-authoritative.
- **Mandatory Human Verification**: All candidate facts must be reviewed by authorized human officers through .NET Component 3 before entering the immutable `VerifiedFactSnapshot`.
- **Zero Case Authority**: Qwen cannot approve, reject, reroute, screen, determine consensus, or complete lease cases.
- **Storage Independence**: Source document bytes and OCR transcripts remain stored behind durable storage abstractions (`IDocumentContentReader` / `IDocumentContentWriter` / `IAnalysisArtifactReader` / `IAnalysisArtifactWriter`), not in PostgreSQL.
