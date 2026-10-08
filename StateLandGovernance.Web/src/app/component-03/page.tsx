import { PageLayout, PageHeading } from "@/shared/components/PageLayout";

export default function Component03Page() {
  return (
    <PageLayout showSidebar={false}>
      <PageHeading title="Component 3" />
      <div className="placeholderPage">
        <p>Coming soon — owned by another component.</p>
      </div>
    </PageLayout>
  );
}
