import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import ts from "typescript";
import { createRequire } from "node:module";

const loadCommonJs = createRequire(import.meta.url);
loadCommonJs.extensions[".ts"] = (module, filename) =>
  module._compile(
    ts.transpileModule(fs.readFileSync(filename, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText,
    filename,
  );

const {
  leaseInterestStatusLabel,
  leaseInterestApplicantOutcome,
  isLeaseInterestDraftStatus,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestStatus.ts",
);
const { leaseInterestPurposeDisplay } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestPurposeDisplay.ts",
);
const { formatRuleScore, formatCriterionScore } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/formatters.ts",
);
const {
  gisCoverageStatusLabel,
  gisCoverageBadgeTone,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/gisCoverageStatus.ts",
);
const {
  isSourceReportedPrivateClaimant,
  leaseInterestEligibilityBlock,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestEligibility.ts",
);
const { convertTargetAreaToHectares } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/targetAreaUnits.ts",
);
const { isLeaseInterestDraftReadyForSubmit } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestReview.ts",
);
const { proposalWithParcelArea } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestProposal.ts",
);
const { leaseInterestMissingParcelAreaBlock } = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestEligibility.ts",
);
const {
  NOT_ENTERED,
  buildValuationWorksheetSections,
  displayMoney,
  displayOptionalText,
  isAgriculturalCommercialValuation,
  isResidentialValuation,
  valuationFormTypeLabel,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/authorityValuationWorksheet.ts",
);
const {
  beginValuationSaveAttempt,
  markValuationSaveFailed,
  markValuationSaveSucceeded,
  preserveValuationFormOnFailure,
  shouldBlockValuationSave,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/officerValuationSave.ts",
);
const {
  CANONICAL_LAND_USE_LABELS,
  buildCanonicalLandUseChoices,
  normalizeLandUseFilterValue,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/canonicalLandUse.ts",
);
const {
  parseRecommendationSearchParams,
  buildRecommendationSearchParams,
  recommendationSearchFingerprint,
  sessionMatchesUrlFingerprint,
  buildRequestFromFormState,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/recommendationSearchState.ts",
);

const {
  classifyApplicantValuationLoadError,
  applicantValuationStateFromResult,
  loadApplicantValuationState,
  VALUATION_ACCESS_DENIED_MESSAGE,
  VALUATION_LOAD_ERROR_MESSAGE,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/applicantValuationLoad.ts",
);
const {
  getJwtExpiryMs,
  isJwtExpired,
  clearLandIntelligenceRoleToken,
  renewLandIntelligenceRoleToken,
  shouldRenewSessionAfterFailure,
  sendWithSessionRetry,
  JWT_EXPIRY_SKEW_MS,
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/landIntelligenceSession.ts",
);

function sampleValuation(overrides = {}) {
  const { worksheet: worksheetOverrides, ...rest } = overrides;
  return {
    id: "val-1",
    leaseInterestRequestId: "req-1",
    landParcelId: "parcel-1",
    revision: 2,
    formType: "AgriculturalCommercial",
    assessedLandValueLkr: 1250000.5,
    assessedAreaHectares: null,
    inspectionDate: null,
    valuationDate: null,
    valuationReference: null,
    issuingOffice: null,
    siteCondition: null,
    cultivationDetails: null,
    residentialImprovements: null,
    projectUseDetails: null,
    otherTypeDetails: null,
    officerNotes: null,
    recordedByOfficerId: "officer-abc",
    recordedAtUtc: "2026-10-17T15:28:46.944Z",
    ...rest,
    worksheet: {
      leaseFileNumber: "LF-001",
      divisionalSecretariat: "Kolonnawa",
      leaseApprovalDate: null,
      leaseCommencementDate: "2026-01-15",
      valuationReportDate: null,
      leaseAgreementDate: null,
      previousAssessedValueLkr: null,
      leaseRatePercent: 4,
      annualLeaseAmountLkr: 50000,
      firstPeriodAmountLkr: null,
      secondPeriodAmountLkr: null,
      secondPeriodAnnualAmountLkr: null,
      circular2018AmountLkr: 1200,
      circular2020SixAdjustmentLkr: null,
      firstPeriodLines: [{ period: "Year 1", amountLkr: 50000 }],
      secondPeriodLines: [],
      worksheetNotes: "",
      ...worksheetOverrides,
    },
  };
}

function submitReadyDraft(overrides = {}) {
  return {
    consentToPreliminaryAssessment: true,
    applicant: {
      kind: 1,
      legalName: "Applicant One",
      contactPhone: "0771234567",
      contactEmail: "a@example.com",
      postalAddress: "Colombo",
      organizationName: null,
      organizationRegistrationNumber: null,
      nationalIdentityCardNumber: "200012345678",
      representativeNationalIdentityCardNumber: null,
    },
    proposal: {
      requestedPurpose: 1,
      plannedActivitiesDescription: "Activities",
      requestedAreaHectares: 0.025,
      desiredStartPeriod: "2027-Q2",
      requestedDurationYears: 5,
      projectSummary: "Summary",
    },
    supporting: { supportingNotes: "" },
    ...overrides,
  };
}

test("lease interest status labels cover officer workflow states", () => {
  assert.equal(leaseInterestStatusLabel(3), "Pending verification");
  assert.equal(leaseInterestStatusLabel(4), "Needs information");
  assert.match(leaseInterestApplicantOutcome(5), /rejected/i);
});

test("GIS coverage badges treat missing data as neutral unavailable", () => {
  assert.equal(gisCoverageStatusLabel("Unavailable"), "Unavailable");
  assert.equal(gisCoverageBadgeTone("Unavailable"), "neutral");
  assert.equal(gisCoverageBadgeTone("Partial"), "warning");
});

test("target area units convert to hectares for API scoring", () => {
  assert.equal(convertTargetAreaToHectares(0.05, "hectares"), 0.05);
  assert.equal(convertTargetAreaToHectares(20, "perches"), 0.05);
  assert.ok(
    Math.abs(convertTargetAreaToHectares(1, "acres") - 0.40468564224) < 1e-9,
  );
});

test("private claimant blocks lease interest eligibility", () => {
  assert.equal(
    isSourceReportedPrivateClaimant({
      sourceReportedClaimantCategoryCode: "3",
    }),
    true,
  );
  const block = leaseInterestEligibilityBlock(
    {
      cadastralSource: { sourceReportedClaimantCategoryCode: "3" },
      availability: null,
    },
    [],
  );
  assert.equal(block?.code, "private-claimant");
});

test("lease interest submit requires positive requested area from parcel", () => {
  const ready = submitReadyDraft();
  assert.equal(isLeaseInterestDraftReadyForSubmit(ready), true);
  const noArea = submitReadyDraft({
    proposal: { ...ready.proposal, requestedAreaHectares: null },
  });
  assert.equal(isLeaseInterestDraftReadyForSubmit(noArea), false);
});

test("proposal syncs requested area from parcel record", () => {
  const proposal = {
    requestedPurpose: 99,
    plannedActivitiesDescription: "",
    requestedAreaHectares: null,
    desiredStartPeriod: "",
    requestedDurationYears: 1,
    projectSummary: "",
  };
  const parcel = {
    area: { value: 0.025, unit: 2 },
  };
  const synced = proposalWithParcelArea(proposal, parcel);
  assert.equal(synced.requestedAreaHectares, 0.025);
});

test("missing parcel area blocks lease interest eligibility", () => {
  const block = leaseInterestMissingParcelAreaBlock({
    area: { value: 0, unit: 2 },
  });
  assert.equal(block?.code, "missing-parcel-area");
});

test("draft default purpose Other is not shown as applicant selection", () => {
  assert.equal(leaseInterestPurposeDisplay(99, 1), null);
  assert.equal(leaseInterestPurposeDisplay(1, 1), "Agricultural");
  assert.equal(leaseInterestPurposeDisplay(99, 3), "Other");
  assert.equal(isLeaseInterestDraftStatus(1), true);
  assert.equal(isLeaseInterestDraftStatus(3), false);
});

test("match score display shows 100 only for exact 100 and truncates near-perfect", () => {
  assert.equal(formatRuleScore(100), "100");
  assert.equal(formatRuleScore(99.999), "99.99");
  assert.equal(formatRuleScore(95.55), "95.55");
  assert.equal(formatCriterionScore(90), "90.00");
});

test("authority agricultural worksheet shows circular fields and Not entered for blanks", () => {
  assert.equal(isAgriculturalCommercialValuation("AgriculturalCommercial"), true);
  assert.equal(valuationFormTypeLabel("AgriculturalCommercial"), "Agricultural / commercial lease worksheet");
  assert.equal(displayOptionalText(null), NOT_ENTERED);
  assert.equal(displayOptionalText("  "), NOT_ENTERED);
  assert.equal(displayMoney(null), NOT_ENTERED);

  const sections = buildValuationWorksheetSections(sampleValuation());
  const figures = sections.find((section) => section.id === "figures");
  const labels = figures.fields.map((field) => field.label);
  assert.ok(labels.includes("01/2018 circular amount (LKR) — manual"));
  assert.ok(labels.includes("2020/6 circular adjustment (LKR) — manual"));
  assert.equal(labels.includes("Second period annual amount (LKR) — manual"), false);

  const circular2020 = figures.fields.find((field) =>
    field.label.startsWith("2020/6 circular"),
  );
  assert.equal(circular2020.value, NOT_ENTERED);

  const dates = sections.find((section) => section.id === "dates");
  const approval = dates.fields.find((field) => field.label === "Lease approval date");
  assert.equal(approval.value, NOT_ENTERED);

  const observations = sections.find((section) => section.id === "observations");
  assert.ok(
    observations.fields.some((field) => field.label === "Cultivation details"),
  );
  assert.equal(
    observations.fields.some((field) => field.label === "Residential improvements"),
    false,
  );

  const notes = sections.find((section) => section.id === "notes");
  assert.equal(
    notes.notes.find((note) => note.label === "Worksheet notes").value,
    NOT_ENTERED,
  );
});

test("authority residential worksheet shows residential figures only", () => {
  assert.equal(isResidentialValuation("Residential"), true);
  const sections = buildValuationWorksheetSections(
    sampleValuation({
      formType: "Residential",
      worksheet: {
        circular2018AmountLkr: 999,
        circular2020SixAdjustmentLkr: 111,
        secondPeriodAnnualAmountLkr: 22000,
      },
    }),
  );
  const figures = sections.find((section) => section.id === "figures");
  const labels = figures.fields.map((field) => field.label);
  assert.ok(labels.includes("Second period annual amount (LKR) — manual"));
  assert.equal(labels.includes("01/2018 circular amount (LKR) — manual"), false);
  assert.equal(
    labels.includes("2020/6 circular adjustment (LKR) — manual"),
    false,
  );

  const secondAnnual = figures.fields.find((field) =>
    field.label.startsWith("Second period annual"),
  );
  assert.match(secondAnnual.value, /22,000\.00/);

  const observations = sections.find((section) => section.id === "observations");
  assert.ok(
    observations.fields.some((field) => field.label === "Residential improvements"),
  );
  assert.equal(
    observations.fields.some((field) => field.label === "Cultivation details"),
    false,
  );
});

test("officer valuation save blocks double-click and preserves fields after failure", () => {
  assert.equal(shouldBlockValuationSave(false), false);
  assert.equal(shouldBlockValuationSave(true), true);

  const first = beginValuationSaveAttempt(
    { idempotencyKey: null, inFlight: false },
    () => "attempt-key-1",
  );
  assert.equal(first.ok, true);
  assert.equal(first.idempotencyKey, "attempt-key-1");
  assert.equal(first.nextState.inFlight, true);

  const blocked = beginValuationSaveAttempt(first.nextState, () => "other-key");
  assert.equal(blocked.ok, false);
  assert.equal(blocked.reason, "in_flight");

  const afterFailure = markValuationSaveFailed(first.nextState);
  assert.equal(afterFailure.inFlight, false);
  assert.equal(afterFailure.idempotencyKey, "attempt-key-1");

  const formBefore = {
    leaseFileNumber: "LF-77",
    assessedLandValueLkr: "1250000.50",
    worksheetNotes: "keep me",
  };
  const formAfterFailure = preserveValuationFormOnFailure(formBefore);
  assert.deepEqual(formAfterFailure, formBefore);

  const retry = beginValuationSaveAttempt(afterFailure, () => "should-not-be-used");
  assert.equal(retry.ok, true);
  assert.equal(retry.idempotencyKey, "attempt-key-1");

  const afterSuccess = markValuationSaveSucceeded();
  assert.equal(afterSuccess.idempotencyKey, null);
  assert.equal(afterSuccess.inFlight, false);

  const nextAttempt = beginValuationSaveAttempt(afterSuccess, () => "attempt-key-2");
  assert.equal(nextAttempt.idempotencyKey, "attempt-key-2");
});

test("recommendation URL parser preserves district, DS, area unit and land-use filters", () => {
  const params = new URLSearchParams(
    "purpose=1&area=2.5&areaValue=2.5&areaUnit=hectares&district=Colombo&province=Western&ds=Colombo&landCover=Coconut&maxResults=30",
  );
  const parsed = parseRecommendationSearchParams(params);
  assert.ok(parsed);
  assert.equal(parsed.request.requiredPurpose, 1);
  assert.equal(parsed.request.preferredLocation.district, "Colombo");
  assert.equal(parsed.request.preferredLocation.province, "Western");
  assert.equal(parsed.request.preferredLocation.divisionalSecretariat, "Colombo");
  assert.equal(parsed.request.researchLandCoverGroup, "Coconut");
  assert.equal(parsed.request.sourceReportedLandUse, null);
  assert.equal(parsed.formState.preferredDistrict, "Colombo");
  assert.equal(parsed.formState.preferredDivisionalSecretariat, "Colombo");
  assert.equal(parsed.formState.sourceReportedLandUse, "cover:Coconut");
  assert.equal(parsed.formState.targetAreaUnit, "hectares");
});

test("recommendation URL round-trip restores Edit search form values", () => {
  const form = {
    requiredPurpose: 4,
    targetAreaValue: "100",
    targetAreaUnit: "perches",
    preferredProvince: "Western",
    preferredDistrict: "Colombo",
    preferredDivisionalSecretariat: "Thimbirigasyaya",
    sourceReportedLandUse: "75",
    maxResults: "20",
  };
  const request = buildRequestFromFormState(form);
  assert.ok(request);
  const built = buildRecommendationSearchParams(request, form);
  assert.equal(built.get("ds"), "Thimbirigasyaya");
  assert.equal(built.get("district"), "Colombo");
  assert.equal(built.get("areaUnit"), "perches");
  assert.equal(built.get("areaValue"), "100");
  assert.equal(built.get("landUse"), "75");

  const reparsed = parseRecommendationSearchParams(built);
  assert.ok(reparsed);
  assert.equal(reparsed.formState.preferredDivisionalSecretariat, "Thimbirigasyaya");
  assert.equal(reparsed.formState.targetAreaValue, "100");
  assert.equal(reparsed.formState.targetAreaUnit, "perches");
  assert.equal(reparsed.formState.sourceReportedLandUse, "75");
  assert.equal(reparsed.request.preferredLocation.divisionalSecretariat, "Thimbirigasyaya");
});

test("recommendation session fingerprint skips duplicate fetch when URL matches", () => {
  const form = {
    requiredPurpose: 1,
    targetAreaValue: "",
    targetAreaUnit: "hectares",
    preferredProvince: "",
    preferredDistrict: "Colombo",
    preferredDivisionalSecretariat: "",
    sourceReportedLandUse: "",
    maxResults: "30",
  };
  const request = buildRequestFromFormState(form);
  const fp = recommendationSearchFingerprint(request, form);
  const fromUrl = parseRecommendationSearchParams(
    buildRecommendationSearchParams(request, form),
  );
  assert.ok(fromUrl);
  assert.equal(sessionMatchesUrlFingerprint(fp, fromUrl.fingerprint), true);
  assert.equal(sessionMatchesUrlFingerprint("other", fromUrl.fingerprint), false);
});

test("optional recommendation URL params remain undefined when absent", () => {
  const parsed = parseRecommendationSearchParams(
    new URLSearchParams("purpose=2&district=Colombo"),
  );
  assert.ok(parsed);
  assert.equal(parsed.request.preferredLocation.divisionalSecretariat, null);
  assert.equal(parsed.request.preferredLocation.province, null);
  assert.equal(parsed.request.targetAreaHectares, null);
  assert.equal(parsed.request.sourceReportedLandUse, null);
  assert.equal(parsed.request.researchLandCoverGroup, null);
  assert.equal(parsed.formState.preferredDivisionalSecretariat, "");
});

// ---------------------------------------------------------------------------
// Prompt 8 — applicant valuation load states
// ---------------------------------------------------------------------------

const applicantSummary = {
  revision: 1,
  recordedAtUtc: "2026-10-17T15:28:46.944Z",
  formType: "Residential",
  assessedLandValueLkr: 900000,
  leaseFileNumber: null,
  divisionalSecretariat: null,
};

test("applicant valuation: 204/null result is 'none', not an error", () => {
  assert.deepEqual(applicantValuationStateFromResult(null), { kind: "none" });
  assert.deepEqual(applicantValuationStateFromResult(undefined), { kind: "none" });
});

test("applicant valuation: summary result is 'available'", async () => {
  const state = await loadApplicantValuationState(async () => applicantSummary);
  assert.equal(state.kind, "available");
  assert.equal(state.summary.assessedLandValueLkr, 900000);
  assert.equal("recordedByOfficerId" in state.summary, false);
});

test("applicant valuation: loader returning null is 'none'", async () => {
  const state = await loadApplicantValuationState(async () => null);
  assert.deepEqual(state, { kind: "none" });
});

test("applicant valuation: 401 and 403 are 'access_denied'", async () => {
  for (const status of [401, 403]) {
    const state = classifyApplicantValuationLoadError({ status });
    assert.equal(state.kind, "access_denied");
    assert.equal(state.message, VALUATION_ACCESS_DENIED_MESSAGE);
  }
});

test("applicant valuation: 5xx, network and unknown failures are retryable 'error'", async () => {
  for (const err of [{ status: 500 }, { status: 503 }, { status: 0 }, new Error("boom"), null]) {
    const state = classifyApplicantValuationLoadError(err);
    assert.equal(state.kind, "error");
    assert.equal(state.retryable, true);
    assert.equal(state.message, VALUATION_LOAD_ERROR_MESSAGE);
  }
});

test("applicant valuation: thrown API failure never masquerades as 'none'", async () => {
  const state = await loadApplicantValuationState(async () => {
    throw Object.assign(new Error("Forbidden"), { status: 403 });
  });
  assert.equal(state.kind, "access_denied");
  const failed = await loadApplicantValuationState(async () => {
    throw Object.assign(new Error("Server"), { status: 500 });
  });
  assert.equal(failed.kind, "error");
});

test("applicant valuation: retry after temporary failure yields 'available'", async () => {
  let calls = 0;
  const load = async () => {
    calls += 1;
    if (calls === 1) {
      throw Object.assign(new Error("Server"), { status: 503 });
    }
    return applicantSummary;
  };
  assert.equal((await loadApplicantValuationState(load)).kind, "error");
  assert.equal((await loadApplicantValuationState(load)).kind, "available");
});

test("applicant valuation: error messages do not leak officer-only terms", () => {
  const text = `${VALUATION_ACCESS_DENIED_MESSAGE} ${VALUATION_LOAD_ERROR_MESSAGE}`;
  assert.doesNotMatch(text, /officer id|officerNotes|recordedByOfficerId/i);
});

// ---------------------------------------------------------------------------
// Prompt 9 — expiry-aware research-demo session recovery
// ---------------------------------------------------------------------------

function base64Url(value) {
  return Buffer.from(JSON.stringify(value))
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

function fakeJwt(payload) {
  return `${base64Url({ alg: "HS256", typ: "JWT" })}.${base64Url(payload)}.signature`;
}

const NOW_MS = Date.UTC(2026, 9, 18, 12, 0, 0);

test("session: valid token is not expired and exp is parsed", () => {
  const exp = Math.floor(NOW_MS / 1000) + 3600;
  const token = fakeJwt({ sub: "x", exp });
  assert.equal(getJwtExpiryMs(token), exp * 1000);
  assert.equal(isJwtExpired(token, NOW_MS), false);
});

test("session: expired token and near-expiry token are detected", () => {
  const past = fakeJwt({ exp: Math.floor(NOW_MS / 1000) - 10 });
  assert.equal(isJwtExpired(past, NOW_MS), true);
  const nearExpiry = fakeJwt({
    exp: Math.floor((NOW_MS + JWT_EXPIRY_SKEW_MS - 1000) / 1000),
  });
  assert.equal(isJwtExpired(nearExpiry, NOW_MS), true);
});

test("session: malformed token is treated as unusable", () => {
  assert.equal(isJwtExpired("not-a-jwt", NOW_MS), true);
  assert.equal(isJwtExpired("a.%%%.c", NOW_MS), true);
  assert.equal(getJwtExpiryMs("a.b"), null);
});

test("session: only 401/403 for a real role renew, and only once", () => {
  assert.equal(shouldRenewSessionAfterFailure(401, "officer", false), true);
  assert.equal(shouldRenewSessionAfterFailure(403, "authority", false), true);
  assert.equal(shouldRenewSessionAfterFailure(401, "applicant", true), false);
  assert.equal(shouldRenewSessionAfterFailure(500, "applicant", false), false);
  assert.equal(shouldRenewSessionAfterFailure(401, "none", false), false);
});

function stubSessionStorage(initial = {}) {
  const store = new Map(Object.entries(initial));
  const previous = globalThis.window;
  globalThis.window = {
    sessionStorage: {
      getItem: (key) => (store.has(key) ? store.get(key) : null),
      setItem: (key, value) => void store.set(key, value),
      removeItem: (key) => void store.delete(key),
    },
  };
  return {
    store,
    restore() {
      if (previous === undefined) {
        delete globalThis.window;
      } else {
        globalThis.window = previous;
      }
    },
  };
}

test("session: clearLandIntelligenceRoleToken removes only that role", () => {
  const officerKey = "component01.landIntelligence.officerAccessToken";
  const authorityKey = "component01.landIntelligence.authorityAccessToken";
  const env = stubSessionStorage({ [officerKey]: "o", [authorityKey]: "a" });
  try {
    clearLandIntelligenceRoleToken("officer");
    assert.equal(env.store.has(officerKey), false);
    assert.equal(env.store.get(authorityKey), "a");
    clearLandIntelligenceRoleToken("none");
    assert.equal(env.store.get(authorityKey), "a");
  } finally {
    env.restore();
  }
});

test("session: failed renewal clears the unusable token", async () => {
  const officerKey = "component01.landIntelligence.officerAccessToken";
  const env = stubSessionStorage({ [officerKey]: "stale" });
  const previousBase = process.env.NEXT_PUBLIC_API_BASE_URL;
  const previousFetch = globalThis.fetch;
  process.env.NEXT_PUBLIC_API_BASE_URL = "http://api.test";
  globalThis.fetch = async () => ({ ok: false, status: 404 });
  try {
    assert.equal(await renewLandIntelligenceRoleToken("officer"), null);
    assert.equal(env.store.has(officerKey), false);
  } finally {
    globalThis.fetch = previousFetch;
    if (previousBase === undefined) {
      delete process.env.NEXT_PUBLIC_API_BASE_URL;
    } else {
      process.env.NEXT_PUBLIC_API_BASE_URL = previousBase;
    }
    env.restore();
  }
});

test("session: successful renewal stores a fresh token", async () => {
  const officerKey = "component01.landIntelligence.officerAccessToken";
  const env = stubSessionStorage({ [officerKey]: "stale" });
  const previousBase = process.env.NEXT_PUBLIC_API_BASE_URL;
  const previousFetch = globalThis.fetch;
  const fresh = fakeJwt({ exp: Math.floor(Date.now() / 1000) + 3600 });
  process.env.NEXT_PUBLIC_API_BASE_URL = "http://api.test";
  globalThis.fetch = async () => ({
    ok: true,
    status: 200,
    json: async () => ({ accessToken: fresh }),
  });
  try {
    assert.equal(await renewLandIntelligenceRoleToken("officer"), fresh);
    assert.equal(env.store.get(officerKey), fresh);
  } finally {
    globalThis.fetch = previousFetch;
    if (previousBase === undefined) {
      delete process.env.NEXT_PUBLIC_API_BASE_URL;
    } else {
      process.env.NEXT_PUBLIC_API_BASE_URL = previousBase;
    }
    env.restore();
  }
});

test("session retry: success path sends once and does not renew", async () => {
  let sends = 0;
  let renews = 0;
  const result = await sendWithSessionRetry(
    "officer",
    async () => (sends += 1, { status: 200 }),
    async () => (renews += 1, "t"),
    () => {},
  );
  assert.equal(sends, 1);
  assert.equal(renews, 0);
  assert.equal(result.retried, false);
});

test("session retry: 401 renews and retries exactly once to success", async () => {
  const statuses = [401, 200];
  let sends = 0;
  let renews = 0;
  const result = await sendWithSessionRetry(
    "applicant",
    async () => ({ status: statuses[sends++] }),
    async () => (renews += 1, "fresh"),
    () => {},
  );
  assert.equal(sends, 2);
  assert.equal(renews, 1);
  assert.equal(result.retried, true);
  assert.equal(result.response.status, 200);
});

test("session retry: persistent 401/403 never retries a second time", async () => {
  for (const status of [401, 403]) {
    let sends = 0;
    let renews = 0;
    const cleared = [];
    const result = await sendWithSessionRetry(
      "authority",
      async () => (sends += 1, { status }),
      async () => (renews += 1, "fresh"),
      (role) => cleared.push(role),
    );
    assert.equal(sends, 2);
    assert.equal(renews, 1);
    assert.equal(result.response.status, status);
    assert.equal(cleared.includes("authority"), status === 401);
  }
});

test("session retry: renewal failure clears session and does not resend", async () => {
  let sends = 0;
  const cleared = [];
  const result = await sendWithSessionRetry(
    "officer",
    async () => (sends += 1, { status: 401 }),
    async () => null,
    (role) => cleared.push(role),
  );
  assert.equal(sends, 1);
  assert.equal(result.renewalFailed, true);
  assert.equal(result.retried, false);
  assert.deepEqual(cleared, ["officer"]);
});

test("session retry: role 'none' never renews", async () => {
  let renews = 0;
  const result = await sendWithSessionRetry(
    "none",
    async () => ({ status: 401 }),
    async () => (renews += 1, "x"),
    () => {},
  );
  assert.equal(renews, 0);
  assert.equal(result.retried, false);
});

function canonicalOption(label, parcelCount) {
  return {
    key: label.toLowerCase(),
    label,
    parcelCount,
    sourceMatchedParcelCount: 0,
    gisMatchedParcelCount: 0,
    bothSourcesParcelCount: 0,
  };
}

test("canonical land-use list has the ten professional options in order", () => {
  assert.deepEqual([...CANONICAL_LAND_USE_LABELS], [
    "Coconut",
    "Wetland",
    "Scrub / Low Jungle",
    "Paddy Field",
    "Homegarden",
    "Garden",
    "Open Waste Land",
    "Barren Land",
    "Rubber",
    "Other",
  ]);
});

test("canonical choices show one list with API distinct counts and no technical text", () => {
  const options = [
    canonicalOption("Coconut", 23),
    canonicalOption("Wetland", 38),
    canonicalOption("Garden", 0),
    canonicalOption("Other", 16),
  ];
  const choices = buildCanonicalLandUseChoices(options);
  assert.deepEqual(
    choices.map((c) => [c.label, c.count]),
    [["Coconut", 23], ["Wetland", 38], ["Other", 16]],
  );
  const coconut = choices.find((c) => c.label === "Coconut");
  assert.equal(coconut.count, 23);
  assert.equal(new Set(choices.map((c) => c.label)).size, choices.length);
  for (const choice of choices) {
    assert.doesNotMatch(choice.label, /project|source|GIS|\(.*\)/i);
  }
});

test("canonical filter values reuse landCover URL semantics without breaking the codec", () => {
  const form = {
    requiredPurpose: 1,
    targetAreaValue: "",
    targetAreaUnit: "hectares",
    preferredProvince: "",
    preferredDistrict: "Colombo",
    preferredDivisionalSecretariat: "",
    sourceReportedLandUse: "cover:Scrub / Low Jungle",
    maxResults: "30",
  };
  const request = buildRequestFromFormState(form);
  assert.equal(request.researchLandCoverGroup, "Scrub / Low Jungle");
  assert.equal(request.sourceReportedLandUse, null);
  const built = buildRecommendationSearchParams(request, form);
  assert.equal(built.get("landCover"), "Scrub / Low Jungle");
  assert.equal(built.get("landUse"), null);
  const reparsed = parseRecommendationSearchParams(built);
  assert.equal(reparsed.formState.sourceReportedLandUse, "cover:Scrub / Low Jungle");
  assert.equal(reparsed.request.researchLandCoverGroup, "Scrub / Low Jungle");
});

test("legacy land-use filter values normalize to canonical form values", () => {
  assert.equal(normalizeLandUseFilterValue("75"), "cover:Coconut");
  assert.equal(normalizeLandUseFilterValue("73"), "cover:Paddy Field");
  assert.equal(normalizeLandUseFilterValue("cover:Scrub / low jungle"), "cover:Scrub / Low Jungle");
  assert.equal(normalizeLandUseFilterValue("cover:Other State parcel"), "cover:Other");
  assert.equal(normalizeLandUseFilterValue("cover:Coconut"), "cover:Coconut");
  assert.equal(normalizeLandUseFilterValue(""), "");
  assert.equal(normalizeLandUseFilterValue("999"), "999");
});
