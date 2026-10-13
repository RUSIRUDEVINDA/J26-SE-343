/** Stable local subject id used when minting development applicant JWTs. */
const STORAGE_KEY = "component01.landIntelligence.ownerId";

export function getLandIntelligenceOwnerId(): string {
  if (typeof window === "undefined") {
    return "";
  }
  let id = window.localStorage.getItem(STORAGE_KEY);
  if (!id) {
    id = crypto.randomUUID();
    window.localStorage.setItem(STORAGE_KEY, id);
  }
  return id;
}

export function landIntelligenceOwnerHeaders(): HeadersInit {
  const id = getLandIntelligenceOwnerId();
  if (!id) {
    return {};
  }
  return { "X-Land-Intelligence-Owner-Id": id };
}
