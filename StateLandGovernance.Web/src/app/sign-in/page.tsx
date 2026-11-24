"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import {
  isLandIntelligencePathAllowed,
  mockLandIntelligenceHome,
  readMockLandIntelligenceSession,
  signInMockLandIntelligence,
} from "@/modules/component-01-land-intelligence/utils/mockLandIntelligenceAuth";
import { HeaderSignInAction } from "@/shared/components/HeaderSignInAction";
import { PageLayout } from "@/shared/components/PageLayout";
import styles from "./sign-in.module.css";

function SignInForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const existing = readMockLandIntelligenceSession();
    if (!existing) {
      return;
    }
    const returnUrl = searchParams.get("returnUrl")?.trim();
    const home = mockLandIntelligenceHome(existing.role);
    const destination =
      returnUrl &&
      returnUrl.startsWith("/land-intelligence") &&
      isLandIntelligencePathAllowed(returnUrl, existing.role)
        ? returnUrl
        : home;
    router.replace(destination);
  }, [router, searchParams]);

  return (
    <section className={styles.background}>
      <div className={styles.card}>
        <span className={styles.badge}>STATE LAND GOVERNANCE</span>
        <h1 className={styles.title}>Sign in</h1>
        <p className={styles.subtitle}>
          Enter your username and password to access your workspace.
        </p>
        {error ? (
          <p className={styles.alert} role="alert">
            {error}
          </p>
        ) : null}
        <form
          className={styles.form}
          onSubmit={async (event) => {
            event.preventDefault();
            setBusy(true);
            setError("");
            try {
              const { role, homePath } = await signInMockLandIntelligence(
                email,
                password,
              );
              const returnUrl = searchParams.get("returnUrl")?.trim();
              const destination =
                returnUrl &&
                returnUrl.startsWith("/land-intelligence") &&
                isLandIntelligencePathAllowed(returnUrl, role)
                  ? returnUrl
                  : homePath;
              router.push(destination);
              router.refresh();
            } catch (e) {
              setError(
                e instanceof Error ? e.message : "Incorrect email or password.",
              );
            } finally {
              setBusy(false);
            }
          }}
        >
          <label className={styles.field}>
            <span className={styles.label}>Username</span>
            <input
              name="username"
              type="email"
              autoComplete="username"
              className={styles.input}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>
          <label className={styles.field}>
            <span className={styles.label}>Password</span>
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              className={styles.input}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          <button className={styles.submit} type="submit" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
        </form>
      </div>
    </section>
  );
}

export default function SignInPage() {
  return (
    <PageLayout
      showSidebar={false}
      showDevelopmentSessionBar={false}
      headerActions={<HeaderSignInAction />}
    >
      <Suspense fallback={null}>
        <SignInForm />
      </Suspense>
    </PageLayout>
  );
}
