import { PageLayout } from "@/shared/components/PageLayout";
import { OcrDemoPanel } from "@/modules/workflow-governance/components/OcrDemoPanel";

export const metadata = {
  title: "Development OCR Demo | State Land Lease Management",
  description: "Temporary development tool for testing the Component 3 governed OCR pipeline.",
};

export default function OcrDemoPage() {
  return (
    <PageLayout showSidebar={false}>
      <OcrDemoPanel />
    </PageLayout>
  );
}
