/**
 * NON-PRODUCTION: fixed demo credentials for Component 1 viva / local UI only.
 * Not real authentication. API JWTs are still minted via the dev session endpoint.
 */
import {
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
  officerLeaseInterestLookupNavItem,
  officerLeaseInterestQueueNavItem,
  type NavItem,
} from "@/shared/constants/navigation";
import {
  clearAllLandIntelligenceTokens,
  ensureApplicantAccessToken,
  ensureOfficerAccessToken,
} from "./landIntelligenceSession";

export type LandIntelligenceMockRole = "customer" | "officer";

export type MockLandIntelligenceSession = {
  role: LandIntelligenceMockRole;
  email: string;
  signedInAt: string;
};

const SESSION_KEY = "component01.mockLandIntelligenceAuth";
const MOCK_AUTH_EVENT = "component01.mockAuth.changed";

/**
 * useSyncExternalStore compares snapshots with Object.is. Parsing sessionStorage
 * on every read would allocate a new object and retrigger render forever.
 */
let sessionSnapshot: {
  raw: string | null;
  value: MockLandIntelligenceSession | null;
} = { raw: null, value: null };

const MOCK_PASSWORD = "123456";

const MOCK_USERS: Record<
  string,
  { role: LandIntelligenceMockRole; email: string }
> = {
  "ravindu@gmail.com": { role: "customer", email: "ravindu@gmail.com" },
  "officer@gmail.com": { role: "officer", email: "officer@gmail.com" },
};

function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

export function mockLandIntelligenceHome(
  role: LandIntelligenceMockRole,
): string {
  return role === "officer"
    ? `${LAND_INTELLIGENCE_BASE}/parcels`
    : LAND_INTELLIGENCE_BASE;
}

function parseStoredSession(
  raw: string,
): MockLandIntelligenceSession | null {
  try {
    const parsed = JSON.parse(raw) as Partial<MockLandIntelligenceSession>;
    if (
      (parsed.role !== "customer" && parsed.role !== "officer") ||
      typeof parsed.email !== "string" ||
      !parsed.email.trim()
    ) {
      return null;
    }
    return {
      role: parsed.role,
      email: parsed.email.trim(),
      signedInAt: parsed.signedInAt ?? "",
    };
  } catch {
    return null;
  }
}

export function readMockLandIntelligenceSession(): MockLandIntelligenceSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  let raw: string | null;
  try {
    raw = window.sessionStorage.getItem(SESSION_KEY);
  } catch {
    return sessionSnapshot.value;
  }

  if (sessionSnapshot.raw === raw) {
    return sessionSnapshot.value;
  }

  const value = raw ? parseStoredSession(raw) : null;
  if (raw && !value) {
    try {
      window.sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // ignore private mode / blocked storage
    }
    raw = null;
  }

  sessionSnapshot = { raw, value };
  return value;
}

function writeMockLandIntelligenceSession(
  session: MockLandIntelligenceSession,
): void {
  const raw = JSON.stringify(session);
  window.sessionStorage.setItem(SESSION_KEY, raw);
  sessionSnapshot = { raw, value: session };
  window.dispatchEvent(new Event(MOCK_AUTH_EVENT));
}

export function clearMockLandIntelligenceSession(): void {
  if (typeof window === "undefined") {
    return;
  }
  window.sessionStorage.removeItem(SESSION_KEY);
  sessionSnapshot = { raw: null, value: null };
  clearAllLandIntelligenceTokens();
  window.dispatchEvent(new Event(MOCK_AUTH_EVENT));
}

export function subscribeMockLandIntelligenceAuth(
  onChange: () => void,
): () => void {
  const handler = () => onChange();
  window.addEventListener(MOCK_AUTH_EVENT, handler);
  window.addEventListener("storage", handler);
  return () => {
    window.removeEventListener(MOCK_AUTH_EVENT, handler);
    window.removeEventListener("storage", handler);
  };
}

export function validateMockCredentials(
  email: string,
  password: string,
): MockLandIntelligenceSession | null {
  const key = normalizeEmail(email);
  const user = MOCK_USERS[key];
  if (!user || password !== MOCK_PASSWORD) {
    return null;
  }
  return {
    role: user.role,
    email: user.email,
    signedInAt: new Date().toISOString(),
  };
}

export async function signInMockLandIntelligence(
  email: string,
  password: string,
): Promise<{ role: LandIntelligenceMockRole; homePath: string }> {
  const session = validateMockCredentials(email, password);
  if (!session) {
    throw new Error("Incorrect email or password.");
  }

  clearAllLandIntelligenceTokens();
  writeMockLandIntelligenceSession(session);

  const token =
    session.role === "officer"
      ? await ensureOfficerAccessToken()
      : await ensureApplicantAccessToken();

  if (!token) {
    clearMockLandIntelligenceSession();
    throw new Error(
      "Could not start a session. Ensure the local API is running with development Land Intelligence auth enabled.",
    );
  }

  return {
    role: session.role,
    homePath: mockLandIntelligenceHome(session.role),
  };
}

const CUSTOMER_SIDEBAR_LABELS = new Set([
  "Dashboard",
  "Find Suitable Land",
  "My requests",
  "Knowledge Graph",
]);

export function landIntelligenceNavItemsForRole(
  role: LandIntelligenceMockRole,
): NavItem[] {
  if (role === "customer") {
    return component01NavItems.filter((item) =>
      CUSTOMER_SIDEBAR_LABELS.has(item.label),
    );
  }
  const parcelIntel = component01NavItems.find(
    (item) => item.label === "Parcel Intelligence",
  );
  const items: NavItem[] = [];
  if (parcelIntel) {
    items.push(parcelIntel);
  }
  items.push(officerLeaseInterestQueueNavItem);
  items.push(officerLeaseInterestLookupNavItem);
  return items;
}

const UUID_SEGMENT =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function isParcelCatalogPath(pathname: string): boolean {
  const normalized = pathname.replace(/\/$/, "") || "/";
  return normalized === `${LAND_INTELLIGENCE_BASE}/parcels`;
}

function isCustomerParcelDetailPath(pathname: string): boolean {
  const prefix = `${LAND_INTELLIGENCE_BASE}/parcels/`;
  if (!pathname.startsWith(prefix)) {
    return false;
  }
  const rest = pathname.slice(prefix.length);
  const segments = rest.split("/").filter(Boolean);
  if (segments.length === 0) {
    return false;
  }
  if (!UUID_SEGMENT.test(segments[0])) {
    return false;
  }
  if (segments.length === 1) {
    return true;
  }
  if (segments.length === 2) {
    return segments[1] === "map" || segments[1] === "lease-interest";
  }
  return false;
}

export function isLandIntelligencePathAllowed(
  pathname: string,
  role: LandIntelligenceMockRole,
): boolean {
  const normalized = pathname.split("?")[0].replace(/\/$/, "") || "/";
  const base = LAND_INTELLIGENCE_BASE.replace(/\/$/, "");

  if (!normalized.startsWith(base)) {
    return false;
  }

  if (role === "officer") {
    if (normalized === base) {
      return false;
    }
    if (isParcelCatalogPath(normalized)) {
      return true;
    }
    if (normalized.startsWith(`${base}/parcel-intelligence`)) {
      return true;
    }
    if (normalized.startsWith(`${base}/officer`)) {
      return true;
    }
    if (normalized.startsWith(`${base}/parcels/`)) {
      return true;
    }
    return false;
  }

  if (normalized === base) {
    return true;
  }
  if (isParcelCatalogPath(normalized)) {
    return false;
  }
  if (normalized.startsWith(`${base}/find-suitable-land`)) {
    return true;
  }
  if (normalized.startsWith(`${base}/recommendations`)) {
    return true;
  }
  if (normalized.startsWith(`${base}/my-requests`)) {
    return true;
  }
  if (normalized.startsWith(`${base}/knowledge-graph`)) {
    return true;
  }
  if (isCustomerParcelDetailPath(normalized)) {
    return true;
  }
  if (normalized.startsWith(`${base}/parcel-intelligence`)) {
    return false;
  }
  if (normalized.startsWith(`${base}/officer`)) {
    return false;
  }
  if (normalized.startsWith(`${base}/authority`)) {
    return false;
  }
  if (normalized.startsWith(`${base}/shortlist`)) {
    return false;
  }
  if (normalized.startsWith(`${base}/parcels`)) {
    return false;
  }

  return false;
}

export function mockLandIntelligenceRoleLabel(
  role: LandIntelligenceMockRole,
): string {
  return role === "officer" ? "Officer" : "Customer";
}
