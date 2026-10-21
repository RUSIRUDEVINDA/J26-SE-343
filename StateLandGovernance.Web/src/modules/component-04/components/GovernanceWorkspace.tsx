"use client";

import { createContext, useContext, useState, type ReactNode } from "react";
import { Button } from "@/shared/components/Button";
import styles from "./GovernanceWorkspace.module.css";

export type GovernanceMode = "live" | "demo";
const ModeContext = createContext<GovernanceMode>("live");
export function useGovernanceMode() { return useContext(ModeContext); }

export function GovernanceWorkspace({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<GovernanceMode>("live");
  return <ModeContext.Provider value={mode}>
    <div className={styles.modeBar}>
      <p role="status"><strong>{mode === "demo" ? "DEMO DATA" : "Live workspace"}</strong> · {mode === "demo" ? "Fictional examples. Nothing is saved to the departmental system." : "Only connected services can supply live results."}</p>
      <Button variant="secondary" onClick={() => setMode(mode === "live" ? "demo" : "live")}>{mode === "live" ? "Explore demo data" : "Return to live"}</Button>
    </div>
    <div key={mode} className={styles.workspace}>{children}</div>
    <p className={styles.footer}>Decision support only. Findings and machine predictions do not constitute final lease approval, legal conclusions or proof of misconduct.</p>
  </ModeContext.Provider>;
}
