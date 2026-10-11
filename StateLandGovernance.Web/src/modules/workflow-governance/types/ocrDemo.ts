/**
 * OCR Demo types matching the backend DTOs in StateLandGovernance.WorkflowGovernance.Presentation.DTOs
 */

export type OcrLanguageMode = "eng" | "sin" | "sin+eng";

export interface OcrDemoPageResponse {
  pageNumber: number;
  rawText: string;
  cleanText: string;
  confidence: number | null;
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
}
