"use client";

import { useRouter } from "next/navigation";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import {
  getLandIntelligenceGisCoverage,
  listCanonicalLandUseOptions,
  listGramaNiladhariDivisionOptions,
  listSourceReportedLandUseOptions,
  listResearchSupplement,
  searchRecommendations,
} from "../services/landIntelligenceApi";
import {
  FIND_SUITABLE_LAND_FORM_SESSION_KEY,
  isSupportedLandUseType,
  LandUseType,
  RECOMMENDATIONS_SESSION_KEY,
  type FindSuitableLandFormState,
  type CanonicalLandUseOptionResponse,
  type IndexedDistrictSummaryResponse,
  type LandIntelligenceGisCoverageResponse,
  type RecommendationsSessionPayload,
  type SourceReportedLandUseOptionResponse,
  type ResearchSupplementParcelResponse,
} from "../types/landIntelligence";
import {
  buildCanonicalLandUseChoices,
  buildCanonicalLandUseFallbackOptions,
  normalizeLandUseFilterValue,
} from "../utils/canonicalLandUse";
import { landUseTypeLabels, landUseTypeOptions } from "../utils/enumMappings";
import {
  buildRecommendationSearchParams,
  buildRequestFromFormState,
  DEFAULT_FIND_SUITABLE_MAX_RESULTS,
  normalizeStoredFindSuitableMaxResults,
} from "../utils/recommendationSearchState";
import {
  convertTargetAreaToHectares,
  parseTargetAreaInputUnit,
  targetAreaUnitOptions,
  targetAreaUnitShortLabel,
} from "../utils/targetAreaUnits";
import styles from "./FindSuitableLandForm.module.css";

const FORM_STORE_EVENT = "component01.findSuitableLandForm";
const initialState: FindSuitableLandFormState = {
  requiredPurpose: "",
  targetAreaValue: "",
  targetAreaUnit: "hectares",
  preferredProvince: "",
  preferredDistrict: "",
  preferredGramaNiladhariDivision: "",
  sourceReportedLandUse: "",
  maxResults: DEFAULT_FIND_SUITABLE_MAX_RESULTS,
};

type FormSnapshot = {
  raw: string | null;
  value: FindSuitableLandFormState;
};

type FieldErrors = Partial<
  Record<
    | "requiredPurpose"
    | "targetAreaValue"
    | "maxResults"
    | "sourceReportedLandUse",
    string
  >
>;

type EvidenceStatus = "Available" | "Partial coverage" | "Unavailable";

let formSnapshot: FormSnapshot = { raw: null, value: initialState };

function parseStoredForm(raw: string | null): FindSuitableLandFormState {
  if (!raw) {
    return initialState;
  }
  try {
    const parsed = JSON.parse(raw) as Partial<
      FindSuitableLandFormState & {
        targetAreaHectares?: string;
        requiredAreaHectares?: string;
        preferredAreaHectares?: string;
      }
    >;
    return {
      requiredPurpose: isSupportedLandUseType(parsed.requiredPurpose)
        ? parsed.requiredPurpose
        : "",
      targetAreaValue:
        parsed.targetAreaValue ??
        parsed.targetAreaHectares ??
        parsed.preferredAreaHectares ??
        parsed.requiredAreaHectares ??
        "",
      targetAreaUnit: parseTargetAreaInputUnit(parsed.targetAreaUnit),
      preferredProvince: parsed.preferredProvince ?? "",
      preferredDistrict: parsed.preferredDistrict ?? "",
      preferredGramaNiladhariDivision:
        parsed.preferredGramaNiladhariDivision ??
        (parsed as { preferredDivisionalSecretariat?: string })
          .preferredDivisionalSecretariat ??
        "",
      sourceReportedLandUse: normalizeLandUseFilterValue(
        parsed.sourceReportedLandUse ?? "",
      ),
      maxResults: normalizeStoredFindSuitableMaxResults(parsed.maxResults),
    };
  } catch {
    return initialState;
  }
}

function getFormSnapshot(): FindSuitableLandFormState {
  return formSnapshot.value;
}

function getServerFormSnapshot(): FindSuitableLandFormState {
  return initialState;
}

function subscribeFormStore(onStoreChange: () => void) {
  const handler = () => onStoreChange();
  window.addEventListener("storage", handler);
  window.addEventListener(FORM_STORE_EVENT, handler);
  return () => {
    window.removeEventListener("storage", handler);
    window.removeEventListener(FORM_STORE_EVENT, handler);
  };
}

function writeFormStore(next: FindSuitableLandFormState | null) {
  formSnapshot = { raw: null, value: next ?? initialState };
  try {
    if (next === null) {
      sessionStorage.removeItem(FIND_SUITABLE_LAND_FORM_SESSION_KEY);
    } else {
      sessionStorage.setItem(
        FIND_SUITABLE_LAND_FORM_SESSION_KEY,
        JSON.stringify(next),
      );
    }
  } catch {
    // ignore private mode
  }
  window.dispatchEvent(new Event(FORM_STORE_EVENT));
}

function resetFormStoreToDefaults() {
  writeFormStore(null);
}

function restoreFormStoreFromSession() {
  try {
    const raw = sessionStorage.getItem(FIND_SUITABLE_LAND_FORM_SESSION_KEY);
    formSnapshot = { raw, value: parseStoredForm(raw) };
    window.dispatchEvent(new Event(FORM_STORE_EVENT));
  } catch {
    resetFormStoreToDefaults();
  }
}

function statusClass(status: EvidenceStatus): string {
  if (status === "Available") {
    return styles.statusAvailable;
  }
  if (status === "Partial coverage") {
    return styles.statusPartial;
  }
  return styles.statusUnavailable;
}

function buildSummary(form: FindSuitableLandFormState): string {
  const parts: string[] = [];
  const district = form.preferredDistrict.trim();
  parts.push(district ? `Searching ${district}` : "Searching all districts");

  if (form.requiredPurpose !== "") {
    parts.push(
      landUseTypeLabels[form.requiredPurpose as LandUseType] ?? "Selected purpose",
    );
  }

  const targetArea = form.targetAreaValue.trim();
  if (targetArea) {
    parts.push(
      `Target ${targetArea} ${targetAreaUnitShortLabel(form.targetAreaUnit)}`,
    );
  }

  return parts.join(" • ");
}

function hasValidTargetArea(form: FindSuitableLandFormState): boolean {
  const areaValue = form.targetAreaValue.trim();
  if (areaValue === "") {
    return false;
  }
  if (!/^[0-9]*\.?[0-9]+$/.test(areaValue)) {
    return false;
  }
  const parsedArea = Number.parseFloat(areaValue);
  if (Number.isNaN(parsedArea) || parsedArea <= 0) {
    return false;
  }
  return convertTargetAreaToHectares(parsedArea, form.targetAreaUnit) !== null;
}

export function FindSuitableLandForm() {
  const router = useRouter();
  const stored = useSyncExternalStore(
    subscribeFormStore,
    getFormSnapshot,
    getServerFormSnapshot,
  );
  const [draft, setDraft] = useState<FindSuitableLandFormState | null>(null);
  const form = draft ?? stored;
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [sourceLandUseOptions, setSourceLandUseOptions] = useState<
    SourceReportedLandUseOptionResponse[]
  >([]);
  const [researchSupplement, setResearchSupplement] = useState<ResearchSupplementParcelResponse[]>([]);
  const [canonicalLandUseOptions, setCanonicalLandUseOptions] = useState<
    CanonicalLandUseOptionResponse[]
  >([]);
  const [sourceLandUseChecked, setSourceLandUseChecked] = useState(false);
  const [gisCoverage, setGisCoverage] = useState<LandIntelligenceGisCoverageResponse | null>(null);
  const [gnDivisionOptions, setGnDivisionOptions] = useState<string[]>([]);
  const [gnOptionsLoading, setGnOptionsLoading] = useState(false);
  const [gnDivisionInput, setGnDivisionInput] = useState("");
  const [gnDivisionOpen, setGnDivisionOpen] = useState(false);
  const [gnDivisionMenuAbove, setGnDivisionMenuAbove] = useState(false);
  const gnDivisionDropdownRef = useRef<HTMLDivElement>(null);
  const gnDivisionInputRef = useRef<HTMLInputElement>(null);
  const [landUseOpen, setLandUseOpen] = useState(false);
  const [landUseMenuAbove, setLandUseMenuAbove] = useState(false);
  const landUseDropdownRef = useRef<HTMLDivElement>(null);
  const landUseTriggerRef = useRef<HTMLButtonElement>(null);

  const persist = useCallback((next: FindSuitableLandFormState) => {
    setDraft(next);
    writeFormStore(next);
  }, []);

  useEffect(() => {
    restoreFormStoreFromSession();
    setDraft(null);
  }, []);

  function updateField<K extends keyof FindSuitableLandFormState>(
    key: K,
    value: FindSuitableLandFormState[K],
  ) {
    persist({ ...form, [key]: value });
    if (key in fieldErrors) {
      setFieldErrors((prev) => {
        const next = { ...prev };
        delete next[key as keyof FieldErrors];
        return next;
      });
    }
  }

  useEffect(() => {
    let cancelled = false;
    Promise.allSettled([
      listSourceReportedLandUseOptions(),
      listResearchSupplement(),
      getLandIntelligenceGisCoverage(),
      listCanonicalLandUseOptions(),
    ]).then(([source, supplement, coverage, canonical]) => {
      if (cancelled) return;
      const sourceOptions = source.status === "fulfilled" ? source.value : [];
      const supplementRows = supplement.status === "fulfilled" ? supplement.value : [];
      setSourceLandUseOptions(sourceOptions);
      setResearchSupplement(supplementRows);
      setCanonicalLandUseOptions(
        canonical.status === "fulfilled"
          ? canonical.value
          : buildCanonicalLandUseFallbackOptions(sourceOptions, supplementRows),
      );
      setGisCoverage(coverage.status === "fulfilled" ? coverage.value : null);
      setSourceLandUseChecked(true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    const district = form.preferredDistrict.trim();
    setGnOptionsLoading(true);
    void listGramaNiladhariDivisionOptions(district || undefined)
      .then((options) => {
        if (!cancelled) {
          setGnDivisionOptions(options);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setGnDivisionOptions([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setGnOptionsLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [form.preferredDistrict]);

  useEffect(() => {
    setGnDivisionInput(form.preferredGramaNiladhariDivision);
  }, [form.preferredGramaNiladhariDivision]);

  const gnDivisionFilter = gnDivisionInput.trim().toLowerCase();
  const filteredGnDivisionOptions = useMemo(() => {
    const matches = gnDivisionFilter
      ? gnDivisionOptions.filter((name) =>
          name.toLowerCase().includes(gnDivisionFilter),
        )
      : gnDivisionOptions;
    return matches.slice(0, 60);
  }, [gnDivisionOptions, gnDivisionFilter]);
  const gnDivisionMatchesTruncated =
    gnDivisionFilter.length > 0 &&
    gnDivisionOptions.filter((name) =>
      name.toLowerCase().includes(gnDivisionFilter),
    ).length > filteredGnDivisionOptions.length;

  useEffect(() => {
    if (!gnDivisionOpen) {
      return;
    }
    const closeOnOutside = (event: PointerEvent | FocusEvent) => {
      if (!gnDivisionDropdownRef.current?.contains(event.target as Node)) {
        setGnDivisionOpen(false);
      }
    };
    document.addEventListener("pointerdown", closeOnOutside);
    document.addEventListener("focusin", closeOnOutside);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutside);
      document.removeEventListener("focusin", closeOnOutside);
    };
  }, [gnDivisionOpen]);

  function showGnDivisionMenu() {
    const rect = gnDivisionInputRef.current?.getBoundingClientRect();
    if (rect) {
      const roomBelow = window.innerHeight - rect.bottom;
      setGnDivisionMenuAbove(
        roomBelow < Math.min(280, window.innerHeight * 0.4) &&
          rect.top > roomBelow,
      );
    }
    setGnDivisionOpen(true);
  }

  function selectGnDivision(name: string) {
    setGnDivisionInput(name);
    updateField("preferredGramaNiladhariDivision", name);
    setGnDivisionOpen(false);
    gnDivisionInputRef.current?.focus();
  }

  function commitGnDivisionInputOnBlur() {
    const trimmed = gnDivisionInput.trim();
    if (!trimmed) {
      updateField("preferredGramaNiladhariDivision", "");
      return;
    }
    const exact = gnDivisionOptions.find(
      (name) => name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (exact) {
      setGnDivisionInput(exact);
      updateField("preferredGramaNiladhariDivision", exact);
      return;
    }
    updateField("preferredGramaNiladhariDivision", "");
  }

  // One canonical list; counts are distinct parcel ids supplied by the API.
  const landUseChoices = buildCanonicalLandUseChoices(canonicalLandUseOptions);
  const normalizedLandUseValue = normalizeLandUseFilterValue(form.sourceReportedLandUse);
  const sourceLandUseDisabled = sourceLandUseChecked && landUseChoices.length === 0;
  const selectedLandUseChoice = landUseChoices.find((choice) => choice.value === normalizedLandUseValue);

  // Stale or legacy stored values are resolved at submit time instead of syncing state in an effect.
  const effectiveLandUseValue = sourceLandUseChecked
    ? (selectedLandUseChoice?.value ?? "")
    : normalizedLandUseValue;

  useEffect(() => {
    if (!landUseOpen) return;
    const closeOnOutsideClick = (event: PointerEvent) => {
      if (!landUseDropdownRef.current?.contains(event.target as Node)) {
        setLandUseOpen(false);
      }
    };
    const closeOnOutsideFocus = (event: FocusEvent) => {
      if (!landUseDropdownRef.current?.contains(event.target as Node)) {
        setLandUseOpen(false);
      }
    };
    document.addEventListener("pointerdown", closeOnOutsideClick);
    document.addEventListener("focusin", closeOnOutsideFocus);
    return () => {
      document.removeEventListener("pointerdown", closeOnOutsideClick);
      document.removeEventListener("focusin", closeOnOutsideFocus);
    };
  }, [landUseOpen]);

  function focusLandUseOption(index: number) {
    const options = landUseDropdownRef.current?.querySelectorAll<HTMLButtonElement>(
      "[data-land-use-option]",
    );
    if (!options?.length) return;
    options[Math.max(0, Math.min(index, options.length - 1))]?.focus();
  }

  function showLandUseMenu(focusIndex: number) {
    const rect = landUseTriggerRef.current?.getBoundingClientRect();
    if (rect) {
      const roomBelow = window.innerHeight - rect.bottom;
      setLandUseMenuAbove(roomBelow < Math.min(340, window.innerHeight * 0.45) && rect.top > roomBelow);
    }
    setLandUseOpen(true);
    requestAnimationFrame(() => focusLandUseOption(focusIndex));
  }

  const availableDistricts = gisCoverage?.indexedDistricts.map(d => d.district) ?? [];

  const evidence: Array<{ label: string; status: EvidenceStatus }> = [];
  if (gisCoverage) {
    evidence.push({
      label: "Parcel records",
      status: gisCoverage.totalParcelCount > 0 ? "Available" : "Unavailable",
    });
    evidence.push({
      label: "Source-reported land use",
      status: !sourceLandUseChecked
        ? "Partial coverage"
        : sourceLandUseOptions.length > 0
          ? "Available"
          : "Unavailable",
    });
    evidence.push({
      label: "Project land-cover classification",
      status: !sourceLandUseChecked
        ? "Partial coverage"
        : researchSupplement.length > 0
          ? "Available"
          : "Unavailable",
    });
    evidence.push({
      label: "Hard legal / environmental restrictions",
      status: gisCoverage.intelligenceLayers.some(l => l.layerKey === "legal_restrictions")
        ? "Available"
        : "Unavailable",
    });
    evidence.push({
      label: "Source-reported claimant screening",
      status: "Available",
    });
  }

  function patchFieldErrors(
    keys: Array<keyof FieldErrors>,
    next: FieldErrors,
  ) {
    setFieldErrors((prev) => {
      const merged = { ...prev };
      for (const key of keys) {
        if (next[key]) {
          merged[key] = next[key];
        } else {
          delete merged[key];
        }
      }
      return merged;
    });
  }

  function validateEssential(): boolean {
    const next: FieldErrors = {};
    if (!isSupportedLandUseType(form.requiredPurpose)) {
      next.requiredPurpose = "Select a required purpose to continue.";
    }
    const areaValue = form.targetAreaValue.trim();
    if (areaValue === "") {
      next.targetAreaValue = "Enter a target area to continue.";
    } else if (!/^[0-9]*\.?[0-9]+$/.test(areaValue)) {
      next.targetAreaValue = "Enter a valid positive number (e.g., 0.05 or 5).";
    } else {
      const parsedArea = Number.parseFloat(areaValue);
      if (Number.isNaN(parsedArea) || parsedArea <= 0) {
        next.targetAreaValue = "Enter a target area greater than zero.";
      } else if (convertTargetAreaToHectares(parsedArea, form.targetAreaUnit) === null) {
        next.targetAreaValue = "Could not convert the target area to hectares.";
      }
    }
    patchFieldErrors(["requiredPurpose", "targetAreaValue"], next);
    return Object.keys(next).length === 0;
  }

  function validateLocation(): boolean {
    const next: FieldErrors = {};
    const maxResultsRaw = form.maxResults.trim();
    // Reject if contains non-integer characters
    if (maxResultsRaw !== "" && !/^[0-9]+$/.test(maxResultsRaw)) {
      next.maxResults = "Result limit must be a whole number from 1 to 100.";
    } else {
      const parsedMaxResults = Number.parseInt(maxResultsRaw, 10);
      if (
        maxResultsRaw === "" ||
        Number.isNaN(parsedMaxResults) ||
        parsedMaxResults < 1 ||
        parsedMaxResults > 100
      ) {
        next.maxResults = "Result limit must be a whole number from 1 to 100.";
      }
    }
    patchFieldErrors(["maxResults"], next);
    return Object.keys(next).length === 0;
  }

  function handleClear() {
    resetFormStoreToDefaults();
    setDraft(null);
    setError(null);
    setFieldErrors({});
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);

    const essentialOk = validateEssential();
    const locationOk = validateLocation();
    if (!essentialOk || !locationOk) {
      return;
    }

    const submitForm: FindSuitableLandFormState = {
      ...form,
      sourceReportedLandUse: effectiveLandUseValue,
    };
    const request = buildRequestFromFormState(submitForm);
    if (!request) {
      return;
    }

    setSubmitting(true);
    try {
      const response = await searchRecommendations(request);
      const payload: RecommendationsSessionPayload = {
        request,
        response,
        submittedAt: new Date().toISOString(),
        formState: submitForm,
      };
      sessionStorage.setItem(
        RECOMMENDATIONS_SESSION_KEY,
        JSON.stringify(payload),
      );
      writeFormStore(submitForm);
      window.dispatchEvent(new Event("component01.recommendations"));

      const params = buildRecommendationSearchParams(request, submitForm);
      router.push(`/land-intelligence/recommendations?${params.toString()}`);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Unable to search suitable lands.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  const summary = buildSummary(form);
  const canSearch =
    isSupportedLandUseType(form.requiredPurpose) &&
    hasValidTargetArea(form) &&
    !submitting;

  return (
    <form className={styles.layout} onSubmit={handleSubmit} noValidate>
      <div className={styles.mainColumn}>
        <div className={styles.sections}>
          <section className={styles.panel} aria-labelledby="essential-heading">
            <h2 id="essential-heading" className={styles.panelTitle}>
              1. Essential
            </h2>
            <p className={styles.panelSubtitle}>
              
            </p>
            <div className={styles.grid}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor="requiredPurpose">
                  Required Purpose{" "}
                  <span className={styles.required} aria-hidden="true">
                    *
                  </span>
                </label>
                <select
                  id="requiredPurpose"
                  className={`${styles.select} ${fieldErrors.requiredPurpose ? styles.selectError : ""}`}
                  value={form.requiredPurpose}
                  onChange={(e) =>
                    updateField(
                      "requiredPurpose",
                      e.target.value === ""
                        ? ""
                        : (Number(e.target.value) as LandUseType),
                    )
                  }
                  required
                  aria-required="true"
                  aria-invalid={Boolean(fieldErrors.requiredPurpose)}
                  aria-describedby="purpose-hint purpose-error"
                >
                  <option value="">Select purpose</option>
                  {landUseTypeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
               
                {fieldErrors.requiredPurpose ? (
                  <p
                    id="purpose-error"
                    className={styles.fieldError}
                    role="alert"
                  >
                    {fieldErrors.requiredPurpose}
                  </p>
                ) : null}
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="targetAreaValue">
                  Target Area{" "}
                  <span className={styles.required} aria-hidden="true">
                    *
                  </span>
                </label>
                <div className={styles.areaWrap}>
                  <input
                    id="targetAreaValue"
                    className={`${styles.input} ${fieldErrors.targetAreaValue ? styles.inputError : ""}`}
                    inputMode="decimal"
                    value={form.targetAreaValue}
                    onChange={(e) =>
                      updateField("targetAreaValue", e.target.value)
                    }
                    placeholder="e.g. 0.05"
                    required
                    aria-required="true"
                    aria-invalid={Boolean(fieldErrors.targetAreaValue)}
                    aria-describedby="target-area-hint target-area-error"
                  />
                  <select
                    id="targetAreaUnit"
                    className={styles.areaUnitSelect}
                    value={form.targetAreaUnit}
                    onChange={(e) =>
                      updateField(
                        "targetAreaUnit",
                        parseTargetAreaInputUnit(e.target.value),
                      )
                    }
                    aria-label="Target area unit"
                  >
                    {targetAreaUnitOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.shortLabel}
                      </option>
                    ))}
                  </select>
                </div>
                {fieldErrors.targetAreaValue ? (
                  <p
                    id="target-area-error"
                    className={styles.fieldError}
                    role="alert"
                  >
                    {fieldErrors.targetAreaValue}
                  </p>
                ) : null}
              </div>
            </div>
          </section>

          <section className={styles.panel} aria-labelledby="location-heading">
            <h2 id="location-heading" className={styles.panelTitle}>
              2. Location 
            </h2>
            <p className={styles.panelSubtitle}>
              
            </p>
            <div className={styles.grid}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor="preferredProvince">
                  Preferred Province
                </label>
                {gisCoverage && gisCoverage.indexedDistricts.length > 0 ? (
                  <select
                    id="preferredProvince"
                    className={styles.select}
                    value={form.preferredProvince}
                    onChange={(e) =>
                      updateField("preferredProvince", e.target.value)
                    }
                  >
                    <option value="">All Provinces</option>
                    {[...new Set(gisCoverage.indexedDistricts.map(d => d.province))].map((province) => (
                      <option key={province} value={province}>
                        {province}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id="preferredProvince"
                    className={styles.input}
                    value={form.preferredProvince}
                    onChange={(e) =>
                      updateField("preferredProvince", e.target.value)
                    }
                    placeholder="e.g. Western"
                  />
                )}
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="preferredDistrict">
                  Preferred District
                </label>
                {availableDistricts.length > 0 ? (
                  <select
                    id="preferredDistrict"
                    className={styles.select}
                    value={form.preferredDistrict}
                    onChange={(e) =>
                      persist({
                        ...form,
                        preferredDistrict: e.target.value,
                        preferredGramaNiladhariDivision: "",
                      })
                    }
                    aria-describedby="district-hint"
                  >
                    <option value="">All Districts</option>
                    {availableDistricts.map((district) => (
                      <option key={district} value={district}>
                        {district}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id="preferredDistrict"
                    className={styles.input}
                    value={form.preferredDistrict}
                    onChange={(e) =>
                      updateField("preferredDistrict", e.target.value)
                    }
                    placeholder="e.g. Colombo (leave blank for all)"
                    aria-describedby="district-hint"
                  />
                )}
                <p id="district-hint" className={styles.hint}>
                  {availableDistricts.length > 0
                    ? ""
                    : "Loading indexed districts..."}
                </p>
              </div>

              <div className={styles.field}>
                <label
                  className={styles.label}
                  htmlFor="preferredGramaNiladhariDivision"
                >
                  Grama Niladhari Division
                </label>
                <div
                  className={styles.landUseDropdown}
                  ref={gnDivisionDropdownRef}
                >
                  <input
                    id="preferredGramaNiladhariDivision"
                    ref={gnDivisionInputRef}
                    type="text"
                    className={styles.input}
                    role="combobox"
                    aria-expanded={gnDivisionOpen}
                    aria-controls="gn-division-options"
                    aria-autocomplete="list"
                    aria-describedby="gn-division-hint"
                    disabled={gnOptionsLoading}
                    placeholder={
                      gnOptionsLoading
                        ? "Loading divisions…"
                        : "Type to search GN division"
                    }
                    value={gnDivisionInput}
                    onChange={(e) => {
                      const value = e.target.value;
                      setGnDivisionInput(value);
                      showGnDivisionMenu();
                      const exact = gnDivisionOptions.find(
                        (name) =>
                          name.toLowerCase() === value.trim().toLowerCase(),
                      );
                      updateField(
                        "preferredGramaNiladhariDivision",
                        exact ?? "",
                      );
                    }}
                    onFocus={() => showGnDivisionMenu()}
                    onBlur={() => {
                      window.setTimeout(() => {
                        if (
                          gnDivisionDropdownRef.current?.contains(
                            document.activeElement,
                          )
                        ) {
                          return;
                        }
                        commitGnDivisionInputOnBlur();
                        setGnDivisionOpen(false);
                      }, 0);
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "Escape") {
                        event.preventDefault();
                        setGnDivisionOpen(false);
                      } else if (
                        event.key === "ArrowDown" &&
                        !gnDivisionOpen &&
                        !gnOptionsLoading
                      ) {
                        event.preventDefault();
                        showGnDivisionMenu();
                      }
                    }}
                  />
                  {gnDivisionOpen && !gnOptionsLoading ? (
                    <div
                      id="gn-division-options"
                      className={`${styles.landUseMenu} ${gnDivisionMenuAbove ? styles.landUseMenuAbove : ""}`}
                      role="listbox"
                      aria-label="Grama Niladhari division"
                    >
                      <button
                        type="button"
                        role="option"
                        className={styles.landUseOption}
                        aria-selected={!form.preferredGramaNiladhariDivision}
                        onMouseDown={(event) => event.preventDefault()}
                        onClick={() => selectGnDivision("")}
                      >
                        <span>Any GN division</span>
                      </button>
                      {filteredGnDivisionOptions.length === 0 ? (
                        <p className={styles.landUseEmpty} role="status">
                          No divisions match your search.
                        </p>
                      ) : (
                        filteredGnDivisionOptions.map((name) => (
                          <button
                            key={name}
                            type="button"
                            role="option"
                            className={styles.landUseOption}
                            aria-selected={
                              form.preferredGramaNiladhariDivision === name
                            }
                            onMouseDown={(event) => event.preventDefault()}
                            onClick={() => selectGnDivision(name)}
                          >
                            <span>{name}</span>
                          </button>
                        ))
                      )}
                      {gnDivisionMatchesTruncated ? (
                        <p className={styles.landUseEmpty} role="status">
                          Keep typing to narrow the list.
                        </p>
                      ) : null}
                    </div>
                  ) : null}
                </div>
                <p id="gn-division-hint" className={styles.hint}>
                  {gnOptionsLoading
                    ? ""
                    : gnDivisionOptions.length === 0
                      ? "No GN divisions for the selected district."
                      : "Type part of the name, then choose a match from the list."}
                </p>
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="maxResults">
                  Maximum Results to Show
                </label>
                <input
                  id="maxResults"
                  className={`${styles.input} ${fieldErrors.maxResults ? styles.inputError : ""}`}
                  inputMode="numeric"
                  value={form.maxResults}
                  onChange={(e) => updateField("maxResults", e.target.value)}
                  aria-invalid={Boolean(fieldErrors.maxResults)}
                  aria-describedby="max-results-hint max-results-error"
                />
                
                {fieldErrors.maxResults ? (
                  <p
                    id="max-results-error"
                    className={styles.fieldError}
                    role="alert"
                  >
                    {fieldErrors.maxResults}
                  </p>
                ) : null}
              </div>
            </div>
          </section>

          <section className={styles.panel} aria-labelledby="source-heading">
            <h2 id="source-heading" className={styles.panelTitle}>
              3. Source Filters
            </h2>
            
            <div className={styles.grid}>
              <div className={`${styles.field} ${styles.fullWidth}`}>
                <label className={styles.label} htmlFor="sourceReportedLandUse">
                  Land Use
                </label>
                <div className={styles.landUseDropdown} ref={landUseDropdownRef}>
                  <button
                    id="sourceReportedLandUse"
                    ref={landUseTriggerRef}
                    type="button"
                    className={`${styles.landUseTrigger} ${fieldErrors.sourceReportedLandUse ? styles.selectError : ""}`}
                    aria-haspopup="listbox"
                    aria-expanded={landUseOpen}
                    aria-controls="source-land-use-options"
                    aria-describedby="source-land-use-hint"
                    disabled={sourceLandUseDisabled}
                    onClick={() => {
                      if (landUseOpen) {
                        setLandUseOpen(false);
                      } else {
                        showLandUseMenu(
                          Math.max(0, landUseChoices.findIndex((choice) => choice.value === normalizedLandUseValue) + 1),
                        );
                      }
                    }}
                    onKeyDown={(event) => {
                      if (event.key === "ArrowDown" && !landUseOpen) {
                        event.preventDefault();
                        showLandUseMenu(0);
                      }
                    }}
                  >
                    <span>{sourceLandUseDisabled
                      ? "Unavailable for current coverage"
                      : selectedLandUseChoice
                        ? `${selectedLandUseChoice.label} (${selectedLandUseChoice.count})`
                        : "All Land Uses"}</span>
                    <span className={styles.landUseChevron} aria-hidden="true">▾</span>
                  </button>
                  {landUseOpen && !sourceLandUseDisabled ? (
                    <div
                      id="source-land-use-options"
                      className={`${styles.landUseMenu} ${landUseMenuAbove ? styles.landUseMenuAbove : ""}`}
                      role="listbox"
                      aria-label="Land use"
                      onKeyDown={(event) => {
                        if (event.key === "Escape") {
                          event.preventDefault();
                          setLandUseOpen(false);
                          landUseTriggerRef.current?.focus();
                        } else if (["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
                          event.preventDefault();
                          const options = landUseDropdownRef.current?.querySelectorAll<HTMLButtonElement>("[data-land-use-option]");
                          const current = Array.from(options ?? []).findIndex((option) => option === document.activeElement);
                          const next = event.key === "Home" ? 0
                            : event.key === "End" ? (options?.length ?? 1) - 1
                              : current + (event.key === "ArrowDown" ? 1 : -1);
                          focusLandUseOption(next);
                        }
                      }}
                    >
                      <button type="button" role="option" data-land-use-option aria-selected={!selectedLandUseChoice}
                        className={styles.landUseOption}
                        onClick={() => { updateField("sourceReportedLandUse", ""); setLandUseOpen(false); landUseTriggerRef.current?.focus(); }}>
                        <span>All land uses</span>
                      </button>
                      {landUseChoices.map((choice) => (
                        <button key={choice.value} type="button" role="option" data-land-use-option
                          aria-selected={selectedLandUseChoice?.value === choice.value}
                          className={styles.landUseOption}
                          onClick={() => { updateField("sourceReportedLandUse", choice.value); setLandUseOpen(false); landUseTriggerRef.current?.focus(); }}>
                          <span>{choice.label}</span><span className={styles.landUseCount}>{choice.count}</span>
                        </button>
                      ))}
                    </div>
                  ) : null}
                </div>
                {sourceLandUseDisabled ? (
                  <p id="source-land-use-hint" className={styles.unavailableNote} role="status">
                    Land-use options could not be loaded. Refresh the page after
                    confirming that the Land Intelligence API is running.
                  </p>
                ) : null}
                {fieldErrors.sourceReportedLandUse ? (
                  <p className={styles.fieldError} role="alert">
                    {fieldErrors.sourceReportedLandUse}
                  </p>
                ) : null}
              </div>
            </div>
          </section>
        </div>

        {error ? (
          <div className={styles.apiError}>
            <ErrorMessage message={error} />
          </div>
        ) : null}
      </div>

      <aside className={styles.sidePanel} aria-labelledby="evidence-heading">
        <h2 id="evidence-heading" className={styles.sideTitle}>
          Evidence used in this search
        </h2>
        <p className={styles.sideSubtitle}>
          {form.preferredDistrict.trim()
            ? `Signals for ${form.preferredDistrict.trim()}.`
            : "Signals across indexed districts."}
        </p>
        {gisCoverage && evidence.length > 0 ? (
          <>
            <ul className={styles.evidenceList}>
              {evidence.map((item) => (
                <li key={item.label} className={styles.evidenceItem}>
                  <span className={styles.evidenceLabel}>{item.label}</span>
                  <span
                    className={`${styles.evidenceStatus} ${statusClass(item.status)}`}
                  >
                    {item.status}
                  </span>
                </li>
              ))}
            </ul>

          </>
        ) : (
          <p className={styles.sideNote}>
            Loading evidence coverage information...
          </p>
        )}
      </aside>

      <div className={styles.actionBar}>
        <p className={styles.summaryText}>
          {form.requiredPurpose === "" ? (
            <span className={styles.summaryMuted}>
              Select a required purpose to enable search.
            </span>
          ) : !hasValidTargetArea(form) ? (
            <span className={styles.summaryMuted}>
              Enter a target area to enable search.
            </span>
          ) : (
            <span>{summary}</span>
          )}
        </p>
        <div className={styles.actionButtons}>
          <Button type="button" variant="secondary" onClick={handleClear}>
            Clear form
          </Button>
          <Button type="submit" variant="primary" disabled={!canSearch}>
            {submitting ? "Searching…" : "Find suitable land"}
          </Button>
        </div>
      </div>
    </form>
  );
}
