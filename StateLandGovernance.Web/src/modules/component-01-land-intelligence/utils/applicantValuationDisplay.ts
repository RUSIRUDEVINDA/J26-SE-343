import type {
  LeaseInterestApplicantValuationSummary,
  LeaseInterestApplicantWorksheetSummary,
  LeaseWorksheetPeriodLine,
} from "../types/landIntelligence";

export const APPLICANT_NOT_RECORDED = "Not recorded";

export type ApplicantEvaluationField = {
  id: string;
  label: string;
  value: string;
};

export type ApplicantEvaluationPeriodTable = {
  id: string;
  title: string;
  rows: { period: string; amount: string }[];
};

export type ApplicantEvaluationBlock = {
  id: string;
  title: string;
  intro?: string;
  fields: ApplicantEvaluationField[];
  tables?: ApplicantEvaluationPeriodTable[];
};

function isBlank(value: unknown): boolean {
  if (value == null) {
    return true;
  }
  if (typeof value === "string") {
    return value.trim().length === 0;
  }
  return false;
}

export function formatApplicantText(value: string | null | undefined): string {
  return isBlank(value) ? APPLICANT_NOT_RECORDED : String(value).trim();
}

export function formatApplicantMoney(value: number | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) {
    return APPLICANT_NOT_RECORDED;
  }
  return `LKR ${Number(value).toLocaleString("en-LK", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function formatApplicantPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) {
    return APPLICANT_NOT_RECORDED;
  }
  return `${Number(value)}%`;
}

export function formatApplicantDate(value: string | null | undefined): string {
  if (isBlank(value)) {
    return APPLICANT_NOT_RECORDED;
  }
  const parsed = new Date(String(value));
  if (Number.isNaN(parsed.getTime())) {
    return String(value);
  }
  return parsed.toLocaleDateString("en-LK", { dateStyle: "medium" });
}

export function formatApplicantDateTime(value: string | null | undefined): string {
  if (isBlank(value)) {
    return APPLICANT_NOT_RECORDED;
  }
  const parsed = new Date(String(value));
  if (Number.isNaN(parsed.getTime())) {
    return String(value);
  }
  return parsed.toLocaleString("en-LK", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export function formatApplicantAreaHectares(
  value: number | null | undefined,
): string {
  if (value == null || Number.isNaN(Number(value))) {
    return APPLICANT_NOT_RECORDED;
  }
  return `${Number(value).toLocaleString("en-LK", {
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  })} ha`;
}

export function applicantValuationFormLabel(formType: string): string {
  if (formType === "Residential") {
    return "Residential lease worksheet";
  }
  if (formType === "AgriculturalCommercial" || formType === "Agricultural") {
    return "Agricultural / commercial lease worksheet";
  }
  return formType;
}

function isResidentialForm(formType: string): boolean {
  return formType === "Residential";
}

function isAgriculturalForm(formType: string): boolean {
  return formType === "AgriculturalCommercial" || formType === "Agricultural";
}

function periodTableRows(
  id: string,
  title: string,
  lines: LeaseWorksheetPeriodLine[] | null | undefined,
): ApplicantEvaluationPeriodTable | null {
  if (!lines?.length) {
    return null;
  }
  const rows = lines
    .filter((row) => !isBlank(row.period) || row.amountLkr != null)
    .map((row) => ({
      period: formatApplicantText(row.period),
      amount: formatApplicantMoney(row.amountLkr),
    }));
  if (rows.length === 0) {
    return null;
  }
  return { id, title, rows };
}

function worksheetField(
  id: string,
  label: string,
  value: string,
): ApplicantEvaluationField {
  return { id, label, value };
}

const OVERVIEW_ALWAYS_VISIBLE = new Set([
  "recorded",
  "formType",
  "assessedValue",
]);

export function isApplicantRecordedValue(value: string): boolean {
  return value !== APPLICANT_NOT_RECORDED;
}

function visibleEvaluationFields(
  fields: ApplicantEvaluationField[],
  alwaysShowIds?: Set<string>,
): ApplicantEvaluationField[] {
  return fields.filter(
    (field) =>
      alwaysShowIds?.has(field.id) === true ||
      isApplicantRecordedValue(field.value),
  );
}

/** Read-only sections for the current officer valuation (stored values only). */
export function buildApplicantEvaluationBlocks(
  summary: LeaseInterestApplicantValuationSummary,
): ApplicantEvaluationBlock[] {
  const worksheet: LeaseInterestApplicantWorksheetSummary | null | undefined =
    summary.worksheet;
  const residential = isResidentialForm(summary.formType);
  const agricultural = isAgriculturalForm(summary.formType);

  const overviewFields: ApplicantEvaluationField[] = [
    worksheetField(
      "recorded",
      "Recorded on system",
      formatApplicantDateTime(summary.recordedAtUtc),
    ),
    worksheetField(
      "formType",
      "Worksheet type",
      applicantValuationFormLabel(summary.formType),
    ),
    worksheetField(
      "assessedValue",
      "Assessed land value",
      formatApplicantMoney(summary.assessedLandValueLkr),
    ),
    worksheetField(
      "assessedArea",
      "Assessed area",
      formatApplicantAreaHectares(summary.assessedAreaHectares),
    ),
    worksheetField(
      "inspectionDate",
      "Inspection date",
      formatApplicantDate(summary.inspectionDate),
    ),
    worksheetField(
      "valuationDate",
      "Valuation date",
      formatApplicantDate(summary.valuationDate),
    ),
    worksheetField(
      "valuationReference",
      "Valuation reference",
      formatApplicantText(summary.valuationReference),
    ),
    worksheetField(
      "issuingOffice",
      "Issuing office",
      formatApplicantText(summary.issuingOffice),
    ),
    worksheetField(
      "leaseFile",
      "Lease file number",
      formatApplicantText(summary.leaseFileNumber ?? worksheet?.leaseFileNumber),
    ),
    worksheetField(
      "division",
      "Divisional Secretariat",
      formatApplicantText(
        summary.divisionalSecretariat ?? worksheet?.divisionalSecretariat,
      ),
    ),
    worksheetField(
      "valuationReportDate",
      "Valuation report date",
      formatApplicantDate(worksheet?.valuationReportDate),
    ),
    worksheetField(
      "leaseApprovalDate",
      "Lease approval date",
      formatApplicantDate(worksheet?.leaseApprovalDate),
    ),
    worksheetField(
      "leaseCommencementDate",
      "Lease commencement date",
      formatApplicantDate(worksheet?.leaseCommencementDate),
    ),
    worksheetField(
      "leaseAgreementDate",
      "Lease agreement date",
      formatApplicantDate(worksheet?.leaseAgreementDate),
    ),
  ];

  const observationFields: ApplicantEvaluationField[] = [
    worksheetField(
      "siteCondition",
      "Site condition",
      formatApplicantText(summary.siteCondition),
    ),
  ];

  if (agricultural) {
    observationFields.push(
      worksheetField(
        "cultivation",
        "Cultivation details",
        formatApplicantText(summary.cultivationDetails),
      ),
    );
  }

  if (residential) {
    observationFields.push(
      worksheetField(
        "residentialImprovements",
        "Residential improvements",
        formatApplicantText(summary.residentialImprovements),
      ),
    );
  }

  observationFields.push(
    worksheetField(
      "projectUse",
      "Project use details",
      formatApplicantText(summary.projectUseDetails),
    ),
    worksheetField(
      "otherType",
      "Other land-use details",
      formatApplicantText(summary.otherTypeDetails),
    ),
  );

  const breakdownFields: ApplicantEvaluationField[] = [
    worksheetField(
      "previousValue",
      "Previous assessed value",
      formatApplicantMoney(worksheet?.previousAssessedValueLkr),
    ),
    worksheetField(
      "leaseRate",
      "Lease rate (%)",
      formatApplicantPercent(worksheet?.leaseRatePercent),
    ),
    worksheetField(
      "annualLease",
      "Annual lease amount",
      formatApplicantMoney(worksheet?.annualLeaseAmountLkr),
    ),
    worksheetField(
      "firstPeriod",
      "First lease period amount",
      formatApplicantMoney(worksheet?.firstPeriodAmountLkr),
    ),
    worksheetField(
      "secondPeriod",
      "Second lease period amount",
      formatApplicantMoney(worksheet?.secondPeriodAmountLkr),
    ),
  ];

  if (residential) {
    breakdownFields.push(
      worksheetField(
        "secondPeriodAnnual",
        "Second period annual amount",
        formatApplicantMoney(worksheet?.secondPeriodAnnualAmountLkr),
      ),
    );
  }

  if (agricultural) {
    breakdownFields.push(
      worksheetField(
        "circular2018",
        "01/2018 circular amount",
        formatApplicantMoney(worksheet?.circular2018AmountLkr),
      ),
      worksheetField(
        "circular2020",
        "2020/6 circular adjustment",
        formatApplicantMoney(worksheet?.circular2020SixAdjustmentLkr),
      ),
    );
  }

  const breakdownTables = [
    periodTableRows(
      "first-period-lines",
      "First period schedule (as recorded)",
      worksheet?.firstPeriodLines,
    ),
    periodTableRows(
      "second-period-lines",
      "Second period schedule (as recorded)",
      worksheet?.secondPeriodLines,
    ),
  ].filter((table): table is ApplicantEvaluationPeriodTable => table != null);

  const blocks: ApplicantEvaluationBlock[] = [
    {
      id: "overview",
      title: "Assessment overview",
      intro:
        "Figures below are taken from the officer valuation on record.",
      fields: visibleEvaluationFields(overviewFields, OVERVIEW_ALWAYS_VISIBLE),
    },
  ];

  const visibleObservations = visibleEvaluationFields(observationFields);
  if (visibleObservations.length > 0) {
    blocks.push({
      id: "observations",
      title: "Land and site observations",
      fields: visibleObservations,
    });
  }

  const visibleBreakdown = visibleEvaluationFields(breakdownFields);
  if (visibleBreakdown.length > 0 || breakdownTables.length > 0) {
    blocks.push({
      id: "breakdown",
      title: "Valuation breakdown",
      intro:
        "Lease amounts and circular figures appear exactly as the officer entered them on the worksheet.",
      fields: visibleBreakdown,
      tables: breakdownTables.length > 0 ? breakdownTables : undefined,
    });
  }

  return blocks;
}
