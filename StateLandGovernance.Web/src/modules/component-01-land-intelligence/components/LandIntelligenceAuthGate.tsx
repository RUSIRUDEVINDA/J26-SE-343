"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useSyncExternalStore, useState, type ReactNode } from "react";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageLayout } from "@/shared/components/PageLayout";
import { LAND_INTELLIGENCE_BASE } from "@/shared/constants/navigation";
import {
  ensureApplicantAccessToken,
  ensureOfficerAccessToken,
} from "../utils/landIntelligenceSession";
import {
  clearMockLandIntelligenceSession,
  isLandIntelligencePathAllowed,
  landIntelligenceNavItemsForRole,
  mockLandIntelligenceHome,
  readMockLandIntelligenceSession,
  subscribeMockLandIntelligenceAuth,
} from "../utils/mockLandIntelligenceAuth";
import { LandIntelligenceMockSessionBar } from "./LandIntelligenceMockSessionBar";

function getServerSession() {
  return null;
}

export function LandIntelligenceAuthGate({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const session = useSyncExternalStore(
    subscribeMockLandIntelligenceAuth,
    readMockLandIntelligenceSession,
    getServerSession,
  );
  const [tokenReady, setTokenReady] = useState(false);

  useEffect(() => {
    if (!session) {
      setTokenReady(false);
      const returnUrl = encodeURIComponent(pathname || LAND_INTELLIGENCE_BASE);
      router.replace(`/sign-in?returnUrl=${returnUrl}`);
      return;
    }

    if (!isLandIntelligencePathAllowed(pathname, session.role)) {
      router.replace(mockLandIntelligenceHome(session.role));
      return;
    }

    let cancelled = false;
    setTokenReady(false);
    const ensure =
      session.role === "officer"
        ? ensureOfficerAccessToken()
        : ensureApplicantAccessToken();

    void ensure.then((token) => {
      if (cancelled) {
        return;
      }
      if (!token) {
        clearMockLandIntelligenceSession();
        router.replace("/sign-in");
        return;
      }
      setTokenReady(true);
    });

    return () => {
      cancelled = true;
    };
  }, [session, pathname, router]);

  if (!session || !tokenReady) {
    return (
      <PageLayout showSidebar={false} showDevelopmentSessionBar={false}>
        <LoadingState message="Loading workspace…" />
      </PageLayout>
    );
  }

  return (
    <PageLayout
      showSidebar
      showDevelopmentSessionBar={false}
      sessionBar={<LandIntelligenceMockSessionBar />}
      navigationItems={landIntelligenceNavItemsForRole(session.role)}
      navigationLabel="Land Intelligence navigation"
      navigationBasePath={LAND_INTELLIGENCE_BASE}
      appendResearchDemoNav={false}
    >
      {children}
    </PageLayout>
  );
}
