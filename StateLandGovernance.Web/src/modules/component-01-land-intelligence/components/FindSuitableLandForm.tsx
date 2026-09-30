"use client";

import { useRouter } from "next/navigation";
import { useCallback, useState, useSyncExternalStore } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { searchRecommendations } from "../services/landIntelligenceApi";
import {
  FIND_SUITABLE_LAND_FORM_SESSION_KEY,
  LandUseType,
  RECOMMENDATIONS_SESSION_KEY,
  type FindSuitableLandFormState,
  type LandCategoryType,
  type RecommendationsSessionPayload,
} from "../types/landIntelligence";
import {
  environmentalPreferenceOptions,
  landCategoryOptions,
  landUseTypeOptions,
  roadDistanceOptions,
} from "../utils/enumMappings";
import styles from "./FindSuitableLandForm.module.css";

const FORM_STORE_EVENT = "component01.findSuitableLandForm";

const initialState: FindSuitableLandFormState = {
  requiredPurpose: "",
  requiredAreaHectares: "",
  preferredProvince: "",
  preferredDistrict: "",
  preferredDivisionalSecretariat: "",
  requiredLandCategory: "",
  maxRoadDistanceMeters: "",
  requireRoadAccess: true,
  environmentalPreference: "conservation",
  rejectProhibitiveEnvironmentalRestrictions: true,
  maxResults: "10",
  waterProximityPreference: "",
  soilGroupPreference: "",
  additionalRequirements: "",
};

type FormSnapshot = {
  raw: string | null;
  value: FindSuitableLandFormState;
};

let formSnapshot: FormSnapshot | null = null;

function parseStoredForm(raw: string | null): FindSuitableLandFormState {
  if (!raw) {
    return initialState;
  }
  try {
    return { ...initialState, ...(JSON.parse(raw) as FindSuitableLandFormState) };
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
  const [validationError, setValidationError] = useState<string | null>(null);

  const persist = useCallback((next: FindSuitableLandFormState) => {
    setDraft(next);
    writeFormStore(next);
  }, []);

  function updateField<K extends keyof FindSuitableLandFormState>(
    key: K,
    value: FindSuitableLandFormState[K],
  ) {
    persist({ ...form, [key]: value });
  }

  function handleClear() {
    setDraft(initialState);
    writeFormStore(null);
    setError(null);
    setValidationError(null);
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setValidationError(null);

    if (form.requiredPurpose === "") {
      setValidationError("Required purpose is mandatory.");
      return;
    }

    const areaValue = form.requiredAreaHectares.trim();
    const parsedArea =
      areaValue === "" ? undefined : Number.parseFloat(areaValue);
    if (areaValue !== "" && (Number.isNaN(parsedArea) || parsedArea! <= 0)) {
      setValidationError(
        "Minimum land area must be a positive number (hectares).",
      );
      return;
    }

    const maxResultsRaw = form.maxResults.trim();
    const parsedMaxResults = Number.parseInt(maxResultsRaw, 10);
    if (
      maxResultsRaw === "" ||
      Number.isNaN(parsedMaxResults) ||
      parsedMaxResults < 1 ||
      parsedMaxResults > 100
    ) {
      setValidationError("Result limit must be an integer between 1 and 100.");
      return;
    }

    const roadOption = roadDistanceOptions.find(
      (option) => option.value === form.maxRoadDistanceMeters,
    );
    const envOption =
      environmentalPreferenceOptions.find(
        (option) => option.value === form.environmentalPreference,
      ) ?? environmentalPreferenceOptions[0];

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
      requiredLandCategory:
        form.requiredLandCategory === ""
          ? null
          : (form.requiredLandCategory as LandCategoryType),
      accessibility: {
        maxRoadDistanceMeters: roadOption?.meters ?? null,
        requireRoadAccess: form.requireRoadAccess,
      },
      environmental: {
        maxAllowedEnvironmentalSeverity: envOption.severity,
        rejectProhibitiveEnvironmentalRestrictions: envOption.rejectProhibitive,
      },
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

  return (
    <form className={styles.card} onSubmit={handleSubmit} noValidate>
      <div className={styles.grid}>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="requiredPurpose">
            Required purpose <span className={styles.required}>*</span>
          </label>
          <select
            id="requiredPurpose"
            className={styles.select}
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
          >
            <option value="">Select purpose</option>
            {landUseTypeOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="preferredDistrict">
            Preferred district
          </label>
          <input
            id="preferredDistrict"
            className={styles.input}
            value={form.preferredDistrict}
            onChange={(e) => updateField("preferredDistrict", e.target.value)}
            placeholder="e.g. Hambantota"
          />
          <p className={styles.hint}>Mapped to preferredLocation.district.</p>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="preferredProvince">
            Preferred province
          </label>
          <input
            id="preferredProvince"
            className={styles.input}
            value={form.preferredProvince}
            onChange={(e) => updateField("preferredProvince", e.target.value)}
            placeholder="e.g. Southern"
          />
        </div>

        <div className={styles.field}>
          <label
            className={styles.label}
            htmlFor="preferredDivisionalSecretariat"
          >
            Preferred divisional secretariat
          </label>
          <input
            id="preferredDivisionalSecretariat"
            className={styles.input}
            value={form.preferredDivisionalSecretariat}
            onChange={(e) =>
              updateField("preferredDivisionalSecretariat", e.target.value)
            }
            placeholder="Optional"
          />
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="requiredAreaHectares">
            Minimum land area
          </label>
          <input
            id="requiredAreaHectares"
            className={styles.input}
            inputMode="decimal"
            value={form.requiredAreaHectares}
            onChange={(e) => updateField("requiredAreaHectares", e.target.value)}
            placeholder="e.g. 10"
            aria-describedby="area-hint"
          />
          <p id="area-hint" className={styles.hint}>
            Enter minimum extent in hectares (ha).
          </p>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="requiredLandCategory">
            Preferred land category
          </label>
          <select
            id="requiredLandCategory"
            className={styles.select}
            value={form.requiredLandCategory}
            onChange={(e) =>
              updateField(
                "requiredLandCategory",
                e.target.value === ""
                  ? ""
                  : (Number(e.target.value) as LandCategoryType),
              )
            }
          >
            {landCategoryOptions.map((option) => (
              <option key={String(option.value)} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="maxRoadDistanceMeters">
            Maximum road distance
          </label>
          <select
            id="maxRoadDistanceMeters"
            className={styles.select}
            value={form.maxRoadDistanceMeters}
            onChange={(e) =>
              updateField("maxRoadDistanceMeters", e.target.value)
            }
          >
            {roadDistanceOptions.map((option) => (
              <option key={option.label} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <p className={styles.hint}>
            Sent as accessibility.maxRoadDistanceMeters (metres).
          </p>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="maxResults">
            Result limit
          </label>
          <input
            id="maxResults"
            className={styles.input}
            inputMode="numeric"
            value={form.maxResults}
            onChange={(e) => updateField("maxResults", e.target.value)}
            aria-describedby="max-results-hint"
          />
          <p id="max-results-hint" className={styles.hint}>
            maxResults — integer 1–100 (API default is 10).
          </p>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="environmentalPreference">
            Environmental preference
          </label>
          <select
            id="environmentalPreference"
            className={styles.select}
            value={form.environmentalPreference}
            onChange={(e) =>
              updateField("environmentalPreference", e.target.value)
            }
          >
            {environmentalPreferenceOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <fieldset className={styles.fieldset}>
            <legend className={styles.label}>Road access requirement</legend>
            <label className={styles.checkLabel}>
              <input
                type="checkbox"
                checked={form.requireRoadAccess}
                onChange={(e) =>
                  updateField("requireRoadAccess", e.target.checked)
                }
              />
              Require road access (accessibility.requireRoadAccess)
            </label>
          </fieldset>
        </div>

        <div className={styles.field}>
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="waterProximity">
              Natural water proximity
            </label>
            <span className={styles.pending}>Not in recommendation API</span>
          </div>
          <select
            id="waterProximity"
            className={styles.select}
            value={form.waterProximityPreference}
            disabled
            aria-disabled="true"
          >
            <option value="">No preference — not submitted</option>
          </select>
          <p className={styles.hint}>
            Disabled: recommendation request DTO has no water-proximity filter.
          </p>
        </div>

        <div className={styles.field}>
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="soilGroupPreference">
              Preferred soil group
            </label>
            <span className={styles.pending}>Not in recommendation API</span>
          </div>
          <select
            id="soilGroupPreference"
            className={styles.select}
            value={form.soilGroupPreference}
            disabled
            aria-disabled="true"
          >
            <option value="">No preference — not submitted</option>
          </select>
        </div>

        <div className={`${styles.field} ${styles.fullWidth}`}>
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="additionalRequirements">
              Additional land requirements
            </label>
            <span className={styles.pending}>Not in recommendation API</span>
          </div>
          <textarea
            id="additionalRequirements"
            className={styles.textarea}
            value={form.additionalRequirements}
            disabled
            aria-disabled="true"
            placeholder="Free-text requirements are not accepted by the current recommendation API."
          />
        </div>
      </div>

      <div className={styles.warning} role="note">
        <strong>GIS data note:</strong> Unavailable spatial data remains
        unavailable and is not treated as safe, low risk, or automatically
        suitable. Experimental Colombo ML stays disabled unless the API is
        explicitly overridden locally.
      </div>

      {validationError ? (
        <ErrorMessage title="Validation" message={validationError} />
      ) : null}
      {error ? <ErrorMessage message={error} /> : null}

      <div className={styles.actions}>
        <Button type="button" variant="secondary" onClick={handleClear}>
          Clear
        </Button>
        <Button type="submit" variant="primary" disabled={submitting}>
          {submitting ? "Searching…" : "Search Suitable Lands ↗"}
        </Button>
      </div>
    </form>
  );
}
