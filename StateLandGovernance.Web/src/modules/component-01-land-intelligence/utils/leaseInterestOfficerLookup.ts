/** Heuristic: Sri Lankan old (9+V/X) or new (12-digit) NIC shapes. */
export function looksLikeSriLankanNic(value: string): boolean {
  const compact = value.replace(/\s/g, "");
  return /^\d{9}[vVxX]$/.test(compact) || /^\d{12}$/.test(compact);
}

export function buildOfficerLeaseInterestLookupRequest(
  identifier: string,
  page = 1,
  pageSize = 10,
): {
  nationalIdentityCardNumber?: string;
  businessRegistrationNumber?: string;
  page: number;
  pageSize: number;
} {
  const trimmed = identifier.trim();
  if (!trimmed) {
    return { page, pageSize };
  }
  if (looksLikeSriLankanNic(trimmed)) {
    return {
      nationalIdentityCardNumber: trimmed,
      page,
      pageSize,
    };
  }

  return {
    businessRegistrationNumber: trimmed,
    page,
    pageSize,
  };
}
