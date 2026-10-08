import { FindSuitableLandForm } from "@/modules/component-01-land-intelligence";
import { PageHeading } from "@/shared/components/PageLayout";

export default function FindSuitableLandPage() {
  return (
    <>
      <PageHeading
        title="Find a Suitable Land"
        subtitle="Enter your required land information to identify suitable state land parcels."
      />
      <FindSuitableLandForm />
    </>
  );
}
