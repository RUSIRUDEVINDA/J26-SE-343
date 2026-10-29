import { FindSuitableLandForm } from "@/modules/component-01-land-intelligence";
import { PageHeading } from "@/shared/components/PageLayout";

export default function FindSuitableLandPage() {
  return (
    <>
      <PageHeading
        title="Find Suitable Land"
        subtitle=""
      />
      <FindSuitableLandForm />
    </>
  );
}
