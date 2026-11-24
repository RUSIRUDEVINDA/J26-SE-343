"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import styles from "./HeaderSignInAction.module.css";

export function HeaderSignInAction() {
  const pathname = usePathname();

  if (pathname === "/sign-in") {
    return (
      <span className={styles.signInButton} aria-current="page">
        Sign in
      </span>
    );
  }

  return (
    <Link href="/sign-in" className={styles.signInButton}>
      Sign in
    </Link>
  );
}
