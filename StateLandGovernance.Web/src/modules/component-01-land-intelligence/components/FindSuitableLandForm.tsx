"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { searchRecommendations } from "../services/landIntelligenceApi";
import {
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

const initialState: FindSuitableLandFormState = {
  requiredPurpose: "",
  requiredAreaHectares: "",
  preferredDistrict: "",
  requiredLandCategory: "",
  maxRoadDistanceMeters: "",
  requireRoadAccess: true,
  environmentalPreference: "conservation",
  rejectProhibitiveEnvironmentalRestrictions: true,
  waterProximityPreference: "",
  soilGroupPreference: "",
  additionalRequirements: "",
};

export function FindSuitableLandForm() {
  const router = useRouter();
  const [form, setForm] = useState<FindSuitableLandFormState>(initialState);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  function updateField<K extends keyof FindSuitableLandFormState>(
    key: K,
    value: FindSuitableLandFormState[K],
  ) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function handleClear() {
    setForm(initialState);
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
      setValidationError("Minimum land area must be a positive number.");
      return;
    }

    const roadOption = roadDistanceOptions.find(
      (option) => option.value === form.maxRoadDistanceMeters,
    );
    const envOption =
      environmentalPreferenceOptions.find(
        (option) => option.value === form.environmentalPreference,
      ) ?? environmentalPreferenceOptions[0];

    const request = {
      requiredPurpose: form.requiredPurpose as LandUseType,
      requiredAreaHectares: parsedArea ?? null,
      preferredLocation: form.preferredDistrict.trim()
        ? { district: form.preferredDistrict.trim() }
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
      maxResults: 10,
    };

    setSubmitting(true);
    try {
      const response = await searchRecommendations(request);
      const payload: RecommendationsSessionPayload = {
        request,
        response,
        submittedAt: new Date().toISOString(),
      };
      sessionStorage.setItem(
        RECOMMENDATIONS_SESSION_KEY,
        JSON.stringify(payload),
      );
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
    <form className={styles.card} onSubmit={handleSubmit}>
      <div className={styles.grid}>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="requiredPurpose">
            Required purpose
          </label>
          <select
            id="requiredPurpose"
            className={styles.select}
            value={form.requiredPurpose}
            onChange={(e) =>
              updateField(
                "requiredPurpose",
                e.target.value === "" ? "" : (Number(e.target.value) as LandUseType),
              )
            }
            required
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
          />
          <p className={styles.hint}>Enter minimum extent in hectares.</p>
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
        </div>

        <div className={styles.field}>
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="waterProximity">
              Natural water proximity
            </label>
            <span className={styles.pending}>GIS preference — API support pending</span>
          </div>
          <select
            id="waterProximity"
            className={styles.select}
            value={form.waterProximityPreference}
            onChange={(e) =>
              updateField("waterProximityPreference", e.target.value)
            }
            disabled
            aria-disabled
          >
            <option value="">No preference</option>
          </select>
          <p className={styles.hint}>
            Natural water proximity only; not utility WaterSupply.
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
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="soilGroupPreference">
              Preferred GIS-derived soil group
            </label>
            <span className={styles.pending}>GIS preference — API support pending</span>
          </div>
          <select
            id="soilGroupPreference"
            className={styles.select}
            value={form.soilGroupPreference}
            onChange={(e) => updateField("soilGroupPreference", e.target.value)}
            disabled
            aria-disabled
          >
            <option value="">No preference</option>
          </select>
        </div>

        <div className={`${styles.field} ${styles.fullWidth}`}>
          <div className={styles.labelRow}>
            <label className={styles.label} htmlFor="additionalRequirements">
              Additional land requirements
            </label>
            <span className={styles.pending}>GIS preference — API support pending</span>
          </div>
          <textarea
            id="additionalRequirements"
            className={styles.textarea}
            value={form.additionalRequirements}
            onChange={(e) =>
              updateField("additionalRequirements", e.target.value)
            }
            placeholder="Describe any additional requirements, preferred location characteristics, access needs, current use preferences, or other relevant information."
            disabled
            aria-disabled
          />
        </div>
      </div>

      <div className={styles.warning} role="note">
        <strong>GIS data note:</strong> Unavailable spatial data will remain
        unavailable and will not be treated as safe, low risk, or automatically
        suitable.
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
