"use client";

import { useRouter } from "next/navigation";
import { useSyncExternalStore } from "react";
import {
  clearMockLandIntelligenceSession,
  readMockLandIntelligenceSession,
  subscribeMockLandIntelligenceAuth,
} from "../utils/mockLandIntelligenceAuth";
import styles from "./LandIntelligenceMockSessionBar.module.css";

function getServerSession() {
  return null;
}

export function LandIntelligenceMockSessionBar() {
  const router = useRouter();
  const session = useSyncExternalStore(
    subscribeMockLandIntelligenceAuth,
    readMockLandIntelligenceSession,
    getServerSession,
  );

  if (!session) {
    return null;
  }

  return (
    <button
      type="button"
      className={styles.signOut}
      onClick={() => {
        clearMockLandIntelligenceSession();
        router.push("/sign-in");
        router.refresh();
      }}
    >
      Sign out
    </button>
  );
}
