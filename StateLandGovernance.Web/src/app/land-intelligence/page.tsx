import { Button } from "@/shared/components/Button";
import { PageHeading } from "@/shared/components/PageLayout";
import { DashboardStats } from "@/modules/component-01-land-intelligence/components/DashboardStats";

export default function Component01DashboardPage() {
  return (
    <>
      <PageHeading
        title="Land Intelligence Dashboard"
        subtitle="Decision-support view of indexed state land parcels for the Colombo pilot. Results inform comparison and review — they are not ownership confirmation or lease approval."
        action={
          <Button
            href="/land-intelligence/find-suitable-land"
            variant="primary"
          >
            Find Suitable Land
          </Button>
        }
      />
      <DashboardStats />
    </>
  );
}
