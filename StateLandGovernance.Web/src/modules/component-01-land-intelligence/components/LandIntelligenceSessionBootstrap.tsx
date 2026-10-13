"use client";

import { useEffect } from "react";
import { ensureApplicantAccessToken } from "../utils/landIntelligenceSession";

export function LandIntelligenceSessionBootstrap() {
  useEffect(() => {
    void ensureApplicantAccessToken();
  }, []);

  return null;
}
