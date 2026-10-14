export const screens = ["overview", "documents", "ocr", "extracted-data", "document-issues", "assessment", "escalation", "prediction", "historical-cases", "proposal-generator", "proposal-editor", "explainable-ai", "proposal-ready"] as const;
export type Screen = (typeof screens)[number];
export const navigation: { screen: Screen; label: string }[] = [
  { screen: "overview", label: "Overview" },
  { screen: "documents", label: "Financial Documents" },
  { screen: "extracted-data", label: "Extracted Data" },
  { screen: "assessment", label: "Financial Assessment" },
  { screen: "prediction", label: "Approval Prediction" },
  { screen: "historical-cases", label: "Historical Insights" },
  { screen: "proposal-generator", label: "Proposal Generator" },
  { screen: "explainable-ai", label: "Explainable AI" },
  { screen: "proposal-ready", label: "My Proposals" },
];
export const screenCopy: Record<Screen, [string, string]> = {
  overview: ["Financial Intelligence & Proposal Support", "Understand financial readiness and build a stronger lease proposal."],
  documents: ["Financial Document Upload", "Organize the evidence needed for your financial assessment."],
  ocr: ["Document Processing", "Follow the document extraction and verification journey."],
  "extracted-data": ["Review Extracted Financial Data", "Check the financial profile before continuing to assessment."],
  "document-issues": ["Documents Requiring Attention", "Resolve missing, unreadable or unverified evidence before assessment."],
  assessment: ["Financial Eligibility Assessment", "An explainable view of financial capacity, stability and credit evidence."],
  escalation: ["High Risk Financial Assessment", "Review the evidence and required clarifications before proceeding."],
  prediction: ["Lease Approval Prediction", "Explore the decision-support view informed by financial and historical evidence."],
  "historical-cases": ["Historical Case Insights", "Explore comparable cases and the evidence behind proposal suggestions."],
  "proposal-generator": ["AI Lease Proposal Generator", "Bring your verified profile, lease purpose and supporting evidence together."],
  "proposal-editor": ["Review and Optimize Proposal", "Review the draft and consider recommendations before finalizing."],
  "explainable-ai": ["Explainable AI Proposal Support", "Understand how evidence supports each recommendation."],
  "proposal-ready": ["Proposal Ready", "Review your checklist and export a draft for independent review."],
};
export const pathFor = (screen: Screen) => screen === "overview" ? "/component-02" : `/component-02/${screen}`;
