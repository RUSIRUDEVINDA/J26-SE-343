# Independent OCR Evaluation Harness (CER / WER)

This evaluation harness benchmarks the raw optical character recognition (OCR) quality of the existing OpenCV preprocessing and Tesseract 5 pipeline independently from downstream semantic extraction, Qwen, or any LLM components.

---

## 1. Directory Structure

```text
document-intelligence-service/
└── evaluation/
    └── ocr/
        ├── ground_truth/
        │   ├── proposal1_page03.txt    # Manually transcribed reference text (Page 3)
        │   ├── proposal1_page05.txt    # Manually transcribed reference text (Page 5)
        │   └── proposal1_page14.txt    # Manually transcribed reference text (Page 14)
        ├── outputs/
        │   ├── proposal1_page03_ocr.txt  # Raw Tesseract OCR output (Page 3)
        │   ├── proposal1_page05_ocr.txt  # Raw Tesseract OCR output (Page 5)
        │   └── proposal1_page14_ocr.txt  # Raw Tesseract OCR output (Page 14)
        ├── evaluate_ocr.py             # Pure-Python Levenshtein CER/WER evaluator & extractor
        └── README.md                   # This documentation
```

---

## 2. Invariants & Research Rules

1. **Ground Truth Integrity**:
   - Reference text in `ground_truth/` MUST be manually transcribed by a human reader directly from the visual document.
   - Ground truth MUST NEVER be populated from OCR outputs, Qwen interpretations, or synthetic generative models.
   - If a reference file is empty or contains a placeholder (`[PLACEHOLDER...]`), the evaluator strictly **refuses** to compute metrics to prevent reporting fabricated accuracy numbers.

2. **Isolated OCR Pipeline**:
   - Extraction bypasses FastAPI web routing, Ollama, Qwen, and Component 4.
   - Invokes `app.ocr.engine.OCREngine` and `app.ocr.pdf_processor.PDFProcessor` directly.

---

## 3. Metric Definitions & Normalization

### Normalization Views

To separate character recognition confusion from spacing and line layout variations, the evaluator produces two metric views:

1. **Strict View**:
   - NFC Unicode Normalization (`unicodedata.normalize("NFC", text)`), preserving base Sinhala glyphs and vowel modifiers.
   - Standard line-break normalization (`\r\n` / `\r` $\rightarrow$ `\n`).
   - Line-by-line whitespace trimming; collapses multi-space runs within lines to single spaces.
   - Retains actual line structure.

2. **Whitespace-Normalized View**:
   - NFC Unicode Normalization.
   - All whitespace sequences (spaces, tabs, newlines) collapsed into single token spaces.
   - Isolates pure lexical and character misrecognitions from layout, line wrapping, and table cell ordering differences.

### Error Rates

- **Character Error Rate (CER)**:
  $$\text{CER} = \frac{S + D + I}{N_{\text{ref\_chars}}}$$
  Where $S$ is character substitutions, $D$ is deletions (characters in reference omitted by OCR), $I$ is insertions (spurious OCR artifacts), and $N_{\text{ref\_chars}}$ is total reference characters.

- **Word Error Rate (WER)**:
  $$\text{WER} = \frac{S_{\text{word}} + D_{\text{word}} + I_{\text{word}}}{N_{\text{ref\_words}}}$$
  Where tokens are whitespace-delimited words.

- **Micro-Aggregates**:
  $$\text{Micro CER} = \frac{\sum (S + D + I)}{\sum N_{\text{ref\_chars}}}$$
  Computed across all evaluated pages by summing raw edit counts and dividing by total reference volume.

---

## 4. Usage Commands (PowerShell)

### Step 1: Generate OCR Outputs for Benchmark Pages

Run the extraction for Proposal1 pages 3, 5, and 14 using the existing Tesseract Sinhala (`sin`) engine:

```powershell
python evaluation/ocr/evaluate_ocr.py --extract --pages 3,5,14 --lang sin
```

Raw outputs will be written to:
- `evaluation/ocr/outputs/proposal1_page03_ocr.txt`
- `evaluation/ocr/outputs/proposal1_page05_ocr.txt`
- `evaluation/ocr/outputs/proposal1_page14_ocr.txt`

### Step 2: Populate Manual Ground Truth

Replace the placeholder text in `ground_truth/proposal1_page03.txt`, `proposal1_page05.txt`, and `proposal1_page14.txt` with verified human transcriptions.

### Step 3: Run CER / WER Evaluation

```powershell
python evaluation/ocr/evaluate_ocr.py --evaluate --pages 3,5,14
```

### Step 4: Combined Command (Extract + Evaluate)

```powershell
python evaluation/ocr/evaluate_ocr.py --pages 3,5,14 --lang sin
```

