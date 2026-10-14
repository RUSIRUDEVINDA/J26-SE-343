# OCR Technology Decision Record

## Context & Goal
Component 2 (AI-Driven Lease Feasibility) requires extracting structured data (Income, Average Account Balance, Loan Obligations) from applicant-uploaded financial documents, specifically:
- Bank Statements (Highly tabular, varied formats)
- Salary Slips (Semi-structured key-value pairs)
- Credit Bureau (CRIB) Reports (Dense, highly structured tables)

We need to select an OCR strategy for our `IDocumentExtractionService` implementation. We compared **Tesseract OCR (Open Source)** against **Cloud OCR Solutions (Azure Document Intelligence / Google Document AI)**.

## Comparison

### 1. Accuracy & Tabular Data Handling
* **Tesseract OCR:** Excellent at reading raw text from flat, clean images. However, it struggles significantly with complex tabular data, multi-column layouts, and nested tables found in bank statements and CRIB reports. Extracting meaningful structure from its raw string output requires writing and maintaining fragile regex or custom NLP parsers for every possible bank format.
* **Cloud OCR (Azure / Google):** These services use advanced multimodal models (like LayoutLM) trained specifically on invoices, receipts, and bank statements. They return highly structured JSON (bounding boxes, identified tables, rows, columns, and key-value pairs) out of the box, offering near-perfect accuracy for our required fields.

### 2. Setup Effort & Maintenance
* **Tesseract OCR:** High effort. Requires installing native dependencies (`libtesseract`), setting up image pre-processing pipelines (deskewing, binarization, noise removal using OpenCV), and continuous maintenance of parsing logic.
* **Cloud OCR:** Low effort. Exposes a clean REST API or SDK. We simply pass the `DocumentUri` and receive structured JSON back. We only need to write the C# wrapper implementing `IDocumentExtractionService`.

### 3. Cost Profile
* **Tesseract OCR:** $0 licensing cost. The only costs are our own compute resources (CPU/Memory) to run the OCR engine, which can be computationally expensive at scale.
* **Cloud OCR:** 
  * **Free Tier:** Typically covers the first 500 pages/month (sufficient for our pilot/dev environments).
  * **Paid Tier:** Roughly $10 to $50 per 1,000 pages (depending on the specific pre-built model used). While not free, the engineering time saved usually outweighs the API costs for moderate volumes.

### 4. Data Privacy & Governance
* **Tesseract OCR:** 100% on-premise/air-gapped capable. Ensures maximum data privacy as no applicant financial data leaves our servers.
* **Cloud OCR:** Requires sending sensitive financial documents to a third-party cloud provider. While enterprise agreements guarantee data is not used for model training, state regulations might require specific regional data residency (e.g., deploying the Azure resource strictly within a local government cloud region).

## Recommendation

**Recommendation: Cloud OCR (Azure Document Intelligence or Google Document AI)**

For the initial implementation and pilot phase, we recommend proceeding with a **Cloud OCR provider**. The complexity of extracting accurate tabular data from varied bank statements using Tesseract is too high and would bottleneck the development of the core Feasibility Scoring Engine. 

By using a cloud provider's pre-built document models, we can immediately retrieve structured financial data and focus on implementing our predictive approval models and generative proposal logic. If strict air-gapped data residency becomes a hard mandate prior to production, we can swap the `IDocumentExtractionService` implementation later (potentially utilizing local, self-hosted LayoutLM containers rather than basic Tesseract).
