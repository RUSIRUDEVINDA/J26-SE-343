import type {
  LeaseInterestValuation,
  LeaseWorksheetPeriodLine,
} from "../types/landIntelligence";

export const NOT_ENTERED = "Not entered";

export type WorksheetField = {
  label: string;
  value: string;
};

export type WorksheetPeriodTable = {
  title: string;
  rows: { period: string; amount: string }[];
};

export type WorksheetSection = {
  id: string;
  title: string;
  fields?: WorksheetField[];
  tables?: WorksheetPeriodTable[];
  notes?: WorksheetField[];
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

export function displayOptionalText(value: string | null | undefined): string {
  if (isBlank(value)) {
    return NOT_ENTERED;
  }
  return String(value).trim();
}

export function displayMoney(value: number | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) {
    return NOT_ENTERED;
  }
  return `LKR ${Number(value).toLocaleString("en-LK", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function displayPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(Number(value))) {
    return NOT_ENTERED;
  }
  return `${Number(value)}%`;
}

export function displayDate(
  value: string | null | undefined,
  withTime = false,
): string {
  if (isBlank(value)) {
    return NOT_ENTERED;
  }
  const parsed = new Date(String(value));
  if (Number.isNaN(parsed.getTime())) {
    return String(value);
  }
  return withTime
    ? parsed.toLocaleString("en-LK", { dateStyle: "medium", timeStyle: "short" })
    : parsed.toLocaleDateString("en-LK", { dateStyle: "medium" });
}

export function valuationFormTypeLabel(formType: string): string {
  if (formType === "Residential") {
    return "Residential lease worksheet";
  }
  if (
    formType === "AgriculturalCommercial" ||
    formType === "Agricultural"
  ) {
    return "Agricultural / commercial lease worksheet";
  }
  return formType;
}

export function isResidentialValuation(formType: string): boolean {
  return formType === "Residential";
}

export function isAgriculturalCommercialValuation(formType: string): boolean {
  return (
    formType === "AgriculturalCommercial" || formType === "Agricultural"
  );
}

function periodRows(
  rows: LeaseWorksheetPeriodLine[] | null | undefined,
): { period: string; amount: string }[] {
  if (!rows?.length) {
    return [{ period: NOT_ENTERED, amount: NOT_ENTERED }];
  }
  const mapped = rows
    .filter((row) => !isBlank(row.period) || row.amountLkr != null)
    .map((row) => ({
      period: displayOptionalText(row.period),
      amount: displayMoney(row.amountLkr),
    }));
  return mapped.length > 0
    ? mapped
    : [{ period: NOT_ENTERED, amount: NOT_ENTERED }];
}

/**
 * Builds read-only worksheet sections for authority review.
 * Does not calculate rates or totals — values are shown exactly as stored.
 */
export function buildValuationWorksheetSections(
  valuation: LeaseInterestValuation,
): WorksheetSection[] {
  const worksheet = valuation.worksheet;
  const residential = isResidentialValuation(valuation.formType);
  const agricultural = isAgriculturalCommercialValuation(valuation.formType);

  const figureFields: WorksheetField[] = [
    {
      label: "Assessed land value (LKR)",
      value: displayMoney(valuation.assessedLandValueLkr),
    },
    {
      label: "Previous assessed value (LKR)",
      value: displayMoney(worksheet?.previousAssessedValueLkr),
    },
    {
      label: "Lease percentage (%) — manual",
      value: displayPercent(worksheet?.leaseRatePercent),
    },
    {
      label: "Annual lease amount (LKR) — manual",
      value: displayMoney(worksheet?.annualLeaseAmountLkr),
    },
    {
      label: "First lease period amount (LKR) — manual",
      value: displayMoney(worksheet?.firstPeriodAmountLkr),
    },
    {
      label: "Second lease period amount (LKR) — manual",
      value: displayMoney(worksheet?.secondPeriodAmountLkr),
    },
  ];

  if (residential) {
    figureFields.push({
      label: "Second period annual amount (LKR) — manual",
      value: displayMoney(worksheet?.secondPeriodAnnualAmountLkr),
    });
  }

  if (agricultural) {
    figureFields.push(
      {
        label: "01/2018 circular amount (LKR) — manual",
        value: displayMoney(worksheet?.circular2018AmountLkr),
      },
      {
        label: "2020/6 circular adjustment (LKR) — manual",
        value: displayMoney(worksheet?.circular2020SixAdjustmentLkr),
      },
    );
  }

  const observationFields: WorksheetField[] = [
    {
      label: "Assessed area (hectares)",
      value:
        valuation.assessedAreaHectares == null
          ? NOT_ENTERED
          : String(valuation.assessedAreaHectares),
    },
    {
      label: "Inspection date",
      value: displayDate(valuation.inspectionDate),
    },
    {
      label: "Valuation date",
      value: displayDate(valuation.valuationDate),
    },
    {
      label: "Valuation reference",
      value: displayOptionalText(valuation.valuationReference),
    },
    {
      label: "Issuing office",
      value: displayOptionalText(valuation.issuingOffice),
    },
    {
      label: "Site condition",
      value: displayOptionalText(valuation.siteCondition),
    },
  ];

  if (agricultural) {
    observationFields.push({
      label: "Cultivation details",
      value: displayOptionalText(valuation.cultivationDetails),
    });
  }

  if (residential) {
    observationFields.push({
      label: "Residential improvements",
      value: displayOptionalText(valuation.residentialImprovements),
    });
  }

  observationFields.push(
    {
      label: "Project use details",
      value: displayOptionalText(valuation.projectUseDetails),
    },
    {
      label: "Other type details",
      value: displayOptionalText(valuation.otherTypeDetails),
    },
  );

  return [
    {
      id: "recording",
      title: "Recording details",
      fields: [
        {
          label: "Paper form type",
          value: valuationFormTypeLabel(valuation.formType),
        },
        { label: "Revision", value: String(valuation.revision) },
        {
          label: "Recorded by officer",
          value: displayOptionalText(valuation.recordedByOfficerId),
        },
        {
          label: "Recorded at",
          value: displayDate(valuation.recordedAtUtc, true),
        },
      ],
    },
    {
      id: "file",
      title: "Lease file and location",
      fields: [
        {
          label: "Lease file number",
          value: displayOptionalText(worksheet?.leaseFileNumber),
        },
        {
          label: "Divisional Secretariat division",
          value: displayOptionalText(worksheet?.divisionalSecretariat),
        },
      ],
    },
    {
      id: "dates",
      title: "Worksheet dates",
      fields: [
        {
          label: "Lease approval date",
          value: displayDate(worksheet?.leaseApprovalDate),
        },
        {
          label: "Lease commencement date",
          value: displayDate(worksheet?.leaseCommencementDate),
        },
        {
          label: "Valuation report issued date",
          value: displayDate(worksheet?.valuationReportDate),
        },
        {
          label: "Lease agreement date",
          value: displayDate(worksheet?.leaseAgreementDate),
        },
      ],
    },
    {
      id: "figures",
      title: "Valuation and lease figures — officer entry (manual)",
      fields: figureFields,
    },
    {
      id: "periods",
      title: "Lease period entries",
      tables: [
        {
          title: "First lease period entries",
          rows: periodRows(worksheet?.firstPeriodLines),
        },
        {
          title: "Second lease period entries",
          rows: periodRows(worksheet?.secondPeriodLines),
        },
      ],
    },
    {
      id: "observations",
      title: "Optional observations",
      fields: observationFields,
    },
    {
      id: "notes",
      title: "Notes",
      notes: [
        {
          label: "Worksheet notes",
          value: displayOptionalText(worksheet?.worksheetNotes),
        },
        {
          label: "Officer notes (internal)",
          value: displayOptionalText(valuation.officerNotes),
        },
      ],
    },
  ];
}
