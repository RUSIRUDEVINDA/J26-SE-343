import { Fragment, type ReactNode } from "react";
import { StatusBadge } from "@/shared/components/StatusBadge";
import styles from "./GovernanceWorkspace.module.css";

export function Unavailable({ title = "Assessment unavailable", children }: { title?: string; children: ReactNode }) {
  return <div className={styles.notice} role="status"><strong>{title}</strong><p>{children}</p></div>;
}
export function Facts({ values }: { values: [string, ReactNode][] }) {
  return <dl className={styles.facts}>{values.map(([label, value]) => <Fragment key={label}><dt>{label}</dt><dd>{value ?? "Unavailable"}</dd></Fragment>)}</dl>;
}
export function Checks({ items }: { items: { label: string; status: string; note?: string }[] }) {
  return <ul className={styles.checks}>{items.map(item => <li key={item.label}><div>{item.label}{item.note && <small>{item.note}</small>}</div><StatusBadge tone={["NotAssessed", "Unavailable", "Missing", "Unverified", "Pending", "InsufficientInformation"].includes(item.status) ? "neutral" : ["Clear", "Compliant", "VerifiedAbsent", "NoConflicts"].includes(item.status) ? "success" : "warning"}>{item.status}</StatusBadge></li>)}</ul>;
}
