import { apiPostFormData } from "@/lib/apiClient";
import type { OcrDemoResponse, OcrLanguageMode, OcrExecutionMode, SemanticProvider } from "../types/ocrDemo";

export async function processOcrDemoDocument(
  file: File,
  languageMode: OcrLanguageMode,
  executionMode: OcrExecutionMode = "ocr",
  semanticProvider: SemanticProvider = "gemini",
): Promise<OcrDemoResponse> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("languageMode", languageMode);
  formData.append("executionMode", executionMode);
  if (executionMode === "analyze") {
    formData.append("semanticProvider", semanticProvider);
  }

  return apiPostFormData<OcrDemoResponse>(
    "/api/workflow-governance/ocr/demo",
    formData,
  );
}
