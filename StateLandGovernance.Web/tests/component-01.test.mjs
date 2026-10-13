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
} = loadCommonJs(
  "../src/modules/component-01-land-intelligence/utils/leaseInterestStatus.ts",
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
  assert.match(leaseInterestApplicantOutcome(5), /declined/i);
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
