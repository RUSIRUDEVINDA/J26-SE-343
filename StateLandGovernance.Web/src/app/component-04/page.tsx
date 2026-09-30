import { PageLayout, PageHeading } from "@/shared/components/PageLayout";

export default function Component04Page() {
  return (
    <PageLayout showSidebar={false}>
      <PageHeading title="Component 4" />
      <div className="placeholderPage">
        <p>Coming soon — owned by another component.</p>
      </div>
    </PageLayout>
  );
}
