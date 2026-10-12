import { apiPostFormData } from "@/lib/apiClient";
import type { OcrDemoResponse, OcrLanguageMode } from "../types/ocrDemo";

export async function processOcrDemoDocument(
  file: File,
  languageMode: OcrLanguageMode,
): Promise<OcrDemoResponse> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("languageMode", languageMode);

  return apiPostFormData<OcrDemoResponse>(
    "/api/workflow-governance/ocr/demo",
    formData,
  );
}
