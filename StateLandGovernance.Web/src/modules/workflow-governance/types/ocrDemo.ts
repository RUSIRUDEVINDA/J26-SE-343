/**
 * OCR Demo types matching the backend DTOs in StateLandGovernance.WorkflowGovernance.Presentation.DTOs
 */

export type OcrLanguageMode = "eng" | "sin" | "sin+eng";
export type OcrExecutionMode = "ocr" | "analyze";
export type SemanticProvider = "gemini" | "qwen";

export interface OcrDemoPageResponse {
  pageNumber: number;
  rawText: string;
  cleanText: string;
  confidence: number | null;
}

export interface DocumentClassificationResponse {
  documentType: string;
  confidence: number | null;
}

export interface CandidateFactResponse {
  factCode: string;
  canonicalValue: string;
  valueKind: string;
  confidence: number | null;
  evidencePage: number | null;
  evidenceQuote: string | null;
  isGrounded: boolean;
  validationWarning: string | null;
  rawValue?: string | null;
  normalizedValue?: string | null;
  isSemanticValid?: boolean;
  validationStatus?: string;
  candidateStatus?: string;
  validationReasons?: string[] | null;
  rank?: number;
  isPreferred?: boolean;
  sourceType?: string | null;
  normalizedEnglishValue?: string | null;
}

export interface GovernedFieldInventoryItem {
  fieldCode: string;
  displayName: string;
  status: string;
  preferredValue?: string | null;
  rawValue?: string | null;
  sourceType?: string | null;
  normalizedEnglishValue?: string | null;
  pageNumber?: number | null;
  confidence?: number | null;
  isGrounded?: boolean;
  candidateCount?: number;
}

export interface ExecutionDurationResponse {
  ocrDurationMs: number | null;
  semanticDurationMs: number | null;
  totalDurationMs: number | null;
}

export interface OcrDemoResponse {
  caseId: string;
  governedDocumentId: string;
  documentVersionId: string;
  documentAnalysisId: string;
  analysisRunId: string;
  status: string;
  languageMode: string;
  fileName: string;
  mediaType: string;
  pageCount: number;
  fullText: string;
  pages: OcrDemoPageResponse[];
  warnings: string[];
  governanceNotice: string;
  executionMode?: string;
  classification?: DocumentClassificationResponse | null;
  candidateFacts?: CandidateFactResponse[] | null;
  allCandidates?: CandidateFactResponse[] | null;
  fieldInventory?: GovernedFieldInventoryItem[] | null;
  candidateCount?: number;
  acceptedCount?: number;
  rejectedCount?: number;
  conflictCount?: number;
  ungroundedCount?: number;
  modelIdentifier?: string | null;
  promptVersion?: string | null;
  durationBreakdown?: ExecutionDurationResponse | null;
  semanticStatus?: string | null;
  semanticProvider?: string | null;
}
