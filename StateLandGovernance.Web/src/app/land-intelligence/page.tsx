import { DashboardStats } from "@/modules/component-01-land-intelligence/components/DashboardStats";
import { PageHeading } from "@/shared/components/PageLayout";

export default function Component01DashboardPage() {
  return (
    <>
      <PageHeading
        title="Land Intelligence"
        subtitle=""
      />
      <DashboardStats />
    </>
  );
}
