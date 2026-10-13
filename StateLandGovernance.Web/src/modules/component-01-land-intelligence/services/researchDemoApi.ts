import { apiGet } from "@/lib/apiClient";
import type { ResearchDemoConfigResponse } from "../types/landIntelligence";

export async function getResearchDemoConfig(): Promise<ResearchDemoConfigResponse> {
  return apiGet<ResearchDemoConfigResponse>("/api/v1/land/research-demo/config");
}
