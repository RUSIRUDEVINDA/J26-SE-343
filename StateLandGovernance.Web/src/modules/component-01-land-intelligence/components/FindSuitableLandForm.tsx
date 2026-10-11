"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import {
  listSourceReportedLandUseOptions,
  searchRecommendations,
} from "../services/landIntelligenceApi";
import {
  FIND_SUITABLE_LAND_FORM_SESSION_KEY,
  LandUseType,
  RECOMMENDATIONS_SESSION_KEY,
  type FindSuitableLandFormState,
  type RecommendationsSessionPayload,
  type SourceReportedLandUseOptionResponse,
} from "../types/landIntelligence";
import { landUseTypeLabels, landUseTypeOptions } from "../utils/enumMappings";
import styles from "./FindSuitableLandForm.module.css";

const FORM_STORE_EVENT = "component01.findSuitableLandForm";

const STEPS = [
  { id: 0, label: "Essential" },
  { id: 1, label: "Location & size" },
  { id: 2, label: "Source filters" },
] as const;

type StepId = (typeof STEPS)[number]["id"];

const initialState: FindSuitableLandFormState = {
  requiredPurpose: "",
  requiredAreaHectares: "",
  preferredProvince: "",
  preferredDistrict: "Colombo",
  preferredDivisionalSecretariat: "",
  sourceReportedLandUse: "",
  maxResults: "10",
};

type FormSnapshot = {
  raw: string | null;
  value: FindSuitableLandFormState;
};

type FieldErrors = Partial<
  Record<
    | "requiredPurpose"
    | "requiredAreaHectares"
    | "maxResults"
    | "sourceReportedLandUse",
    string
  >
>;

type EvidenceStatus = "Available" | "Partial coverage" | "Unavailable";

let formSnapshot: FormSnapshot | null = null;

function parseStoredForm(raw: string | null): FindSuitableLandFormState {
  if (!raw) {
    return initialState;
  }
  try {
    const parsed = JSON.parse(raw) as Partial<FindSuitableLandFormState>;
    return {
      requiredPurpose: parsed.requiredPurpose ?? "",
      requiredAreaHectares: parsed.requiredAreaHectares ?? "",
      preferredProvince: parsed.preferredProvince ?? "",
      preferredDistrict: parsed.preferredDistrict ?? "Colombo",
      preferredDivisionalSecretariat:
        parsed.preferredDivisionalSecretariat ?? "",
      sourceReportedLandUse: parsed.sourceReportedLandUse ?? "",
      maxResults: parsed.maxResults ?? "10",
    };
  } catch {
    return initialState;
  }
}

function getFormSnapshot(): FindSuitableLandFormState {
  const raw = sessionStorage.getItem(FIND_SUITABLE_LAND_FORM_SESSION_KEY);
  if (formSnapshot && formSnapshot.raw === raw) {
    return formSnapshot.value;
  }
  const value = parseStoredForm(raw);
  formSnapshot = { raw, value };
  return value;
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
  try {
    if (next === null) {
      sessionStorage.removeItem(FIND_SUITABLE_LAND_FORM_SESSION_KEY);
      formSnapshot = { raw: null, value: initialState };
    } else {
      const raw = JSON.stringify(next);
      sessionStorage.setItem(FIND_SUITABLE_LAND_FORM_SESSION_KEY, raw);
      formSnapshot = { raw, value: next };
    }
  } catch {
    formSnapshot = { raw: null, value: next ?? initialState };
  }
  window.dispatchEvent(new Event(FORM_STORE_EVENT));
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

  const area = form.requiredAreaHectares.trim();
  if (area) {
    parts.push(`Minimum ${area} ha`);
  }

  return parts.join(" • ");
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
  const [step, setStep] = useState<StepId>(0);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [sourceLandUseOptions, setSourceLandUseOptions] = useState<
    SourceReportedLandUseOptionResponse[]
  >([]);
  const [sourceLandUseChecked, setSourceLandUseChecked] = useState(false);

  const persist = useCallback((next: FindSuitableLandFormState) => {
    setDraft(next);
    writeFormStore(next);
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
    listSourceReportedLandUseOptions()
      .then((options) => {
        if (!cancelled) {
          setSourceLandUseOptions(options);
          setSourceLandUseChecked(true);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSourceLandUseOptions([]);
          setSourceLandUseChecked(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const sourceLandUseDisabled =
    sourceLandUseChecked && sourceLandUseOptions.length === 0;

  const evidence: Array<{ label: string; status: EvidenceStatus }> = [
    {
      label: "Parcel records",
      status: form.preferredDistrict.trim() ? "Available" : "Partial coverage",
    },
    {
      label: "Source-reported land use",
      status: !sourceLandUseChecked
        ? "Partial coverage"
        : sourceLandUseOptions.length > 0
          ? "Available"
          : "Unavailable",
    },
    {
      label: "Hard legal / environmental restrictions",
      status: "Partial coverage",
    },
    {
      label: "Source-reported claimant screening",
      status: "Available",
    },
  ];

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
    if (form.requiredPurpose === "") {
      next.requiredPurpose = "Select a required purpose to continue.";
    }
    const areaValue = form.requiredAreaHectares.trim();
    const parsedArea =
      areaValue === "" ? undefined : Number.parseFloat(areaValue);
    if (areaValue !== "" && (Number.isNaN(parsedArea) || parsedArea! <= 0)) {
      next.requiredAreaHectares =
        "Enter a minimum area greater than zero (hectares).";
    }
    patchFieldErrors(["requiredPurpose", "requiredAreaHectares"], next);
    return Object.keys(next).length === 0;
  }

  function validateLocation(): boolean {
    const next: FieldErrors = {};
    const maxResultsRaw = form.maxResults.trim();
    const parsedMaxResults = Number.parseInt(maxResultsRaw, 10);
    if (
      maxResultsRaw === "" ||
      Number.isNaN(parsedMaxResults) ||
      parsedMaxResults < 1 ||
      parsedMaxResults > 100
    ) {
      next.maxResults = "Result limit must be a whole number from 1 to 100.";
    }
    patchFieldErrors(["maxResults"], next);
    return Object.keys(next).length === 0;
  }

  function validateSourceFilters(): boolean {
    const next: FieldErrors = {};
    if (form.sourceReportedLandUse && sourceLandUseOptions.length === 0) {
      next.sourceReportedLandUse =
        "Unavailable for the selected district because this layer has not been imported.";
    }
    patchFieldErrors(["sourceReportedLandUse"], next);
    return Object.keys(next).length === 0;
  }

  function handleClear() {
    setDraft(initialState);
    writeFormStore(null);
    setError(null);
    setFieldErrors({});
    setStep(0);
  }

  function goBack() {
    setError(null);
    setStep((current) => (current > 0 ? ((current - 1) as StepId) : current));
  }

  function goContinue() {
    setError(null);
    if (step === 0) {
      if (!validateEssential()) {
        return;
      }
      setStep(1);
      return;
    }
    if (step === 1) {
      if (!validateLocation()) {
        return;
      }
      setStep(2);
    }
  }

  function goToStep(nextStep: StepId) {
    if (nextStep === step) {
      return;
    }
    if (nextStep > step) {
      if (step === 0 && !validateEssential()) {
        return;
      }
      if (step === 1 && nextStep === 2 && !validateLocation()) {
        return;
      }
      if (nextStep === 2 && (!validateEssential() || !validateLocation())) {
        return;
      }
    }
    setStep(nextStep);
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);

    const essentialOk = validateEssential();
    const locationOk = validateLocation();
    const sourceOk = validateSourceFilters();
    if (!essentialOk || !locationOk || !sourceOk) {
      if (!essentialOk) {
        setStep(0);
      } else if (!locationOk) {
        setStep(1);
      } else {
        setStep(2);
      }
      return;
    }

    const areaValue = form.requiredAreaHectares.trim();
    const parsedArea =
      areaValue === "" ? undefined : Number.parseFloat(areaValue);
    const parsedMaxResults = Number.parseInt(form.maxResults.trim(), 10);

    const province = form.preferredProvince.trim();
    const district = form.preferredDistrict.trim();
    const ds = form.preferredDivisionalSecretariat.trim();

    const request = {
      requiredPurpose: form.requiredPurpose as LandUseType,
      requiredAreaHectares: parsedArea ?? null,
      preferredLocation:
        province || district || ds
          ? {
              province: province || null,
              district: district || null,
              divisionalSecretariat: ds || null,
            }
          : null,
      sourceReportedLandUse:
        sourceLandUseOptions.length > 0
          ? form.sourceReportedLandUse.trim() || null
          : null,
      maxResults: parsedMaxResults,
    };

    setSubmitting(true);
    try {
      const response = await searchRecommendations(request);
      const payload: RecommendationsSessionPayload = {
        request,
        response,
        submittedAt: new Date().toISOString(),
        formState: form,
      };
      sessionStorage.setItem(
        RECOMMENDATIONS_SESSION_KEY,
        JSON.stringify(payload),
      );
      window.dispatchEvent(new Event("component01.recommendations"));
      writeFormStore(form);
      router.push("/land-intelligence/recommendations");
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
  const canSearch = form.requiredPurpose !== "" && !submitting;

  return (
    <form className={styles.layout} onSubmit={handleSubmit} noValidate>
      <div className={styles.mainColumn}>
        <ol className={styles.steps} aria-label="Form steps">
          {STEPS.map((item) => {
            const active = step === item.id;
            const done = step > item.id;
            return (
              <li key={item.id}>
                <button
                  type="button"
                  className={`${styles.stepTab} ${active ? styles.stepTabActive : ""} ${done ? styles.stepTabDone : ""}`}
                  aria-current={active ? "step" : undefined}
                  onClick={() => goToStep(item.id)}
                >
                  <span className={styles.stepNum} aria-hidden="true">
                    {item.id + 1}
                  </span>
                  {item.label}
                </button>
              </li>
            );
          })}
        </ol>

        {step === 0 ? (
          <section className={styles.panel} aria-labelledby="essential-heading">
            <h2 id="essential-heading" className={styles.panelTitle}>
              1. Essential
            </h2>
            <p className={styles.panelSubtitle}>
              Start with the purpose you need and an optional minimum area.
              Required purpose must be selected before search.
            </p>
            <div className={styles.grid}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor="requiredPurpose">
                  Required purpose{" "}
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
                <p id="purpose-hint" className={styles.hint}>
                  Required purpose affects ranking based on available parcel
                  evidence.
                </p>
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
                <label className={styles.label} htmlFor="requiredAreaHectares">
                  Minimum land area
                  <span className={styles.optionalTag}>Optional</span>
                </label>
                <div className={styles.areaWrap}>
                  <input
                    id="requiredAreaHectares"
                    className={`${styles.input} ${fieldErrors.requiredAreaHectares ? styles.inputError : ""}`}
                    inputMode="decimal"
                    value={form.requiredAreaHectares}
                    onChange={(e) =>
                      updateField("requiredAreaHectares", e.target.value)
                    }
                    placeholder="e.g. 0.5"
                    aria-invalid={Boolean(fieldErrors.requiredAreaHectares)}
                    aria-describedby="area-hint area-error"
                  />
                  <span className={styles.areaSuffix} aria-hidden="true">
                    ha
                  </span>
                </div>
                <p id="area-hint" className={styles.hint}>
                  Parcels smaller than this value are excluded from results.
                </p>
                {fieldErrors.requiredAreaHectares ? (
                  <p id="area-error" className={styles.fieldError} role="alert">
                    {fieldErrors.requiredAreaHectares}
                  </p>
                ) : null}
              </div>
            </div>
          </section>
        ) : null}

        {step === 1 ? (
          <section className={styles.panel} aria-labelledby="location-heading">
            <h2 id="location-heading" className={styles.panelTitle}>
              2. Location &amp; size
            </h2>
            <p className={styles.panelSubtitle}>
              Narrow the search by administrative location and how many ranked
              results to return.
            </p>
            <div className={styles.grid}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor="preferredDistrict">
                  Preferred district
                  <span className={styles.optionalTag}>Optional</span>
                </label>
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
                <p id="district-hint" className={styles.hint}>
                  Case-insensitive district filter. Unsupported districts are
                  not remapped to Colombo.
                </p>
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="preferredProvince">
                  Preferred province
                  <span className={styles.optionalTag}>Optional</span>
                </label>
                <input
                  id="preferredProvince"
                  className={styles.input}
                  value={form.preferredProvince}
                  onChange={(e) =>
                    updateField("preferredProvince", e.target.value)
                  }
                  placeholder="e.g. Western"
                />
              </div>

              <div className={styles.field}>
                <label
                  className={styles.label}
                  htmlFor="preferredDivisionalSecretariat"
                >
                  Divisional secretariat
                  <span className={styles.optionalTag}>Optional</span>
                </label>
                <input
                  id="preferredDivisionalSecretariat"
                  className={styles.input}
                  value={form.preferredDivisionalSecretariat}
                  onChange={(e) =>
                    updateField(
                      "preferredDivisionalSecretariat",
                      e.target.value,
                    )
                  }
                  placeholder="Optional"
                />
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor="maxResults">
                  Maximum results to show
                  <span className={styles.optionalTag}>Optional</span>
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
                <p id="max-results-hint" className={styles.hint}>
                  Limits how many ranked parcels are returned (1–100).
                </p>
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
        ) : null}

        {step === 2 ? (
          <section className={styles.panel} aria-labelledby="source-heading">
            <h2 id="source-heading" className={styles.panelTitle}>
              3. Source filters
            </h2>
            <p className={styles.panelSubtitle}>
              Optionally filter by source-reported land use. Known legal and
              environmental hard restrictions are always applied. Road, water,
              and soil distance preferences are not part of this search.
            </p>
            <div className={styles.grid}>
              <div className={`${styles.field} ${styles.fullWidth}`}>
                <label className={styles.label} htmlFor="sourceReportedLandUse">
                  Source-reported land use
                  <span className={styles.optionalTag}>Optional</span>
                </label>
                <select
                  id="sourceReportedLandUse"
                  className={styles.select}
                  value={
                    sourceLandUseDisabled ? "" : form.sourceReportedLandUse
                  }
                  onChange={(e) =>
                    updateField("sourceReportedLandUse", e.target.value)
                  }
                  disabled={sourceLandUseDisabled}
                  aria-disabled={sourceLandUseDisabled}
                  aria-describedby="source-land-use-hint"
                >
                  <option value="">
                    {sourceLandUseDisabled
                      ? "Unavailable for current coverage"
                      : "All source-reported land uses"}
                  </option>
                  {sourceLandUseOptions.map((option) => (
                    <option key={option.code} value={option.code}>
                      {option.englishLabel}
                    </option>
                  ))}
                </select>
                {sourceLandUseDisabled ? (
                  <p className={styles.unavailableNote} role="status">
                    Unavailable for the selected district because this layer has
                    not been imported.
                  </p>
                ) : (
                  <p id="source-land-use-hint" className={styles.hint}>
                    Source-reported land use comes from cadastral data and does
                    not prove legal availability.
                  </p>
                )}
                {fieldErrors.sourceReportedLandUse ? (
                  <p className={styles.fieldError} role="alert">
                    {fieldErrors.sourceReportedLandUse}
                  </p>
                ) : null}
              </div>
            </div>
          </section>
        ) : null}

        <div className={styles.warning} role="note">
          <strong>Decision-support only:</strong> Results compare recorded
          parcels for review. They do not prove ownership, lease availability,
          or legal suitability. Official confirmation is required. Known hard
          legal and environmental restrictions are still rejected.
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
            : "Signals across indexed districts."}{" "}
          GIS road, water, and soil layers may appear on parcel details but are
          not used as search filters here.
        </p>
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
        <p className={styles.sideNote}>
          Partial coverage means some parcels may lack that layer. Missing
          evidence is never treated as a positive match.
        </p>
      </aside>

      <div className={styles.actionBar}>
        <p className={styles.summaryText}>
          {form.requiredPurpose === "" ? (
            <span className={styles.summaryMuted}>
              Select a required purpose to enable search.
            </span>
          ) : (
            <span>{summary}</span>
          )}
        </p>
        <div className={styles.actionButtons}>
          <Button type="button" variant="secondary" onClick={handleClear}>
            Clear form
          </Button>
          {step > 0 ? (
            <Button type="button" variant="secondary" onClick={goBack}>
              Back
            </Button>
          ) : null}
          {step < 2 ? (
            <Button type="button" variant="primary" onClick={goContinue}>
              Continue
            </Button>
          ) : (
            <Button type="submit" variant="primary" disabled={!canSearch}>
              {submitting ? "Searching…" : "Find suitable land"}
            </Button>
          )}
        </div>
      </div>
    </form>
  );
}
