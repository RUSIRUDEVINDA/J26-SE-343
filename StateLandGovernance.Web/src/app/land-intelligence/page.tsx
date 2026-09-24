import { Button } from "@/shared/components/Button";
import { PageHeading } from "@/shared/components/PageLayout";
import { DashboardStats } from "@/modules/component-01-land-intelligence/components/DashboardStats";

export default function Component01DashboardPage() {
  return (
    <>
      <PageHeading
        title="Land Intelligence Dashboard"
        subtitle="Hambantota pilot — spatial recommendation and knowledge graph"
        action={
          <Button
            href="/land-intelligence/find-suitable-land"
            variant="primary"
          >
            Find Suitable Land ↗
          </Button>
        }
      />
      <DashboardStats />
    </>
  );
}
