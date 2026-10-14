import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { moduleLandingLinks } from "@/shared/constants/navigation";

export default function HomePage() {
  return (
    <PageLayout showSidebar={false}>
      <PageHeading
        title="State Land Lease Information and Management System"
        subtitle="Select a platform module. Explore Land Intelligence or the Financial Intelligence design preview."
      />
      <div className="landingGrid">
        {moduleLandingLinks.map((module) =>
          module.available ? (
            <Link key={module.id} href={module.href} className="landingCard">
              <h2>{module.title}</h2>
              <p>{module.id === "component-02" ? "Open the financial assessment and proposal design preview." : "Open module dashboard and spatial recommendation tools."}</p>
            </Link>
          ) : (
            <div
              key={module.id}
              className="landingCard landingCardDisabled"
              aria-disabled
            >
              <h2>{module.title}</h2>
              <p>Coming soon — owned by another component.</p>
            </div>
          ),
        )}
      </div>
    </PageLayout>
  );
}
