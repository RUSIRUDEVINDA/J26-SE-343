/** Client-side hint only — server validates and normalizes NIC. */
export function maskNic(value: string | null | undefined): string {
  const trimmed = value?.trim();
  if (!trimmed) {
    return "—";
  }
  if (trimmed.length <= 4) {
    return "*".repeat(trimmed.length);
  }
  return "*".repeat(trimmed.length - 4) + trimmed.slice(-4);
}
