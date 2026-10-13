/** Units offered on Find Suitable Land target area (converted to ha for the API). */
export type TargetAreaInputUnit = "hectares" | "acres" | "perches";

/** Sri Lankan square perch (25 m²), consistent with common cadastre practice. */
const SQUARE_METERS_PER_PERCH = 25;
const SQUARE_METERS_PER_HECTARE = 10_000;
const HECTARES_PER_ACRE = 0.40468564224;

export const targetAreaUnitOptions: Array<{
  value: TargetAreaInputUnit;
  label: string;
  shortLabel: string;
}> = [
  { value: "hectares", label: "Hectares (ha)", shortLabel: "ha" },
  { value: "perches", label: "Perches (25 m²)", shortLabel: "perch" },
  { value: "acres", label: "Acres (ac)", shortLabel: "ac" },
];

export function targetAreaUnitShortLabel(unit: TargetAreaInputUnit): string {
  return (
    targetAreaUnitOptions.find((option) => option.value === unit)?.shortLabel ??
    unit
  );
}

export function convertTargetAreaToHectares(
  value: number,
  unit: TargetAreaInputUnit,
): number | null {
  if (Number.isNaN(value) || value <= 0) {
    return null;
  }
  switch (unit) {
    case "hectares":
      return value;
    case "acres":
      return value * HECTARES_PER_ACRE;
    case "perches":
      return (value * SQUARE_METERS_PER_PERCH) / SQUARE_METERS_PER_HECTARE;
    default:
      return null;
  }
}

export function parseTargetAreaInputUnit(
  raw: string | undefined | null,
): TargetAreaInputUnit {
  if (raw === "acres" || raw === "perches" || raw === "hectares") {
    return raw;
  }
  return "hectares";
}
