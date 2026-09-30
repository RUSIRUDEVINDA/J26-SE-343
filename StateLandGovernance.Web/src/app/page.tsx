import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { moduleLandingLinks } from "@/shared/constants/navigation";

export default function HomePage() {
  return (
    <PageLayout showSidebar={false}>
      <PageHeading
        title="State Land Lease Information and Management System"
        subtitle="Select a platform module. Component 1 — Land Intelligence and Spatial Recommendation is available in this frontend."
      />
      <div className="landingGrid">
        {moduleLandingLinks.map((module) =>
          module.available ? (
            <Link key={module.id} href={module.href} className="landingCard">
              <h2>{module.title}</h2>
              <p>Open module dashboard and spatial recommendation tools.</p>
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
