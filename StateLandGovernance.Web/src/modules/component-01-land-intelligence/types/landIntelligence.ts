import type { TargetAreaInputUnit } from "../utils/targetAreaUnits";

export type { TargetAreaInputUnit };

export enum LandUseType {
  Agricultural = 1,
  Commercial = 2,
  Industrial = 3,
  Residential = 4,
  Tourism = 5,
  Conservation = 6,
  MixedUse = 7,
  Other = 99,
}

export const supportedLandUseTypes = [
  LandUseType.Agricultural,
  LandUseType.Residential,
  LandUseType.Commercial,
] as const;

export function isSupportedLandUseType(
  value: unknown,
): value is (typeof supportedLandUseTypes)[number] {
  return (
    typeof value === "number" &&
    supportedLandUseTypes.some((purpose) => purpose === value)
  );
}

export enum LandCategoryType {
  StateLand = 1,
  CrownLand = 2,
  ReservedLand = 3,
  Other = 99,
}

export enum RestrictionSeverity {
  Low = 1,
  Medium = 2,
  High = 3,
  Prohibitive = 4,
}

export enum SpatialConstraintType {
  Zoning = 1,
  BufferZone = 2,
  Setback = 3,
  LandUsePlan = 4,
  Other = 99,
}

export enum AreaUnit {
  SquareMeters = 1,
  Hectares = 2,
  Acres = 3,
}

export enum CriterionCategory {
  SoilSuitability = 1,
  InfrastructureAccess = 2,
  EnvironmentalCompatibility = 3,
  ZoningCompliance = 4,
  EconomicPotential = 5,
  HistoricalPerformance = 6,
  RequiredArea = 7,
  LandCategoryMatch = 8,
  LandUseMatch = 9,
  LocationPreference = 10,
  AccessibilityRequirement = 11,
  EnvironmentalRequirement = 12,
  RegulatoryRequirement = 13,
  SpatialConstraintImpact = 14,
  CustomCriterion = 99,
}

export enum AttributeProvenanceSourceType {
  Official = 1,
  ExternalAuthoritative = 2,
  Derived = 3,
  Imputed = 4,
  Synthetic = 5,
  Unknown = 99,
}

export interface PreferredLocationCriteria {
  province?: string | null;
  district?: string | null;
  divisionalSecretariat?: string | null;
  gramaNiladhariDivision?: string | null;
}

export interface LandRecommendationSearchRequest {
  requiredPurpose: LandUseType;
  /** Target area (ha) for continuous area-match scoring. */
  targetAreaHectares?: number | null;
  /** @deprecated Legacy alias — mapped to targetAreaHectares on the API when target is unset. */
  requiredAreaHectares?: number | null;
  /** @deprecated Legacy alias — mapped to targetAreaHectares when target and required are unset. */
  preferredAreaHectares?: number | null;
  areaTolerancePercent?: number;
  preferredLocation?: PreferredLocationCriteria | null;
  /** Legacy domain category — not used for real Colombo cadastre. */
  requiredLandCategory?: LandCategoryType | null;
  sourceReportedLandUse?: string | null;
  researchLandCoverGroup?: string | null;
  maxResults?: number;
}

export interface AttributeProvenanceResponse {
  sourceType: AttributeProvenanceSourceType;
  sourceName: string;
  confidence?: number | null;
  collectedAt?: string | null;
  isVerified: boolean;
}

export interface CriterionEvaluationResponse {
  key: string;
  name: string;
  category: CriterionCategory;
  isMet: boolean;
  score: number;
  weight: number;
  weightedScore: number;
  summary: string;
  dataProvenance?: AttributeProvenanceResponse | null;
  attributePath?: string | null;
  wasAssessed?: boolean;
  notAssessedReason?: string | null;
}

export interface RecommendationFilterStageResponse {
  name: string;
  candidateCount: number;
  description: string;
}

export interface RecommendationUnavailableCriterionResponse {
  key: string;
  reason: string;
}

export interface RecommendationSearchTransparencyResponse {
  appliedFilters: string[];
  candidateCountBeforeFilters: number;
  candidateCountAfterFilters: number;
  filterStages: RecommendationFilterStageResponse[];
  unavailableCriteria: RecommendationUnavailableCriterionResponse[];
}

export interface RestrictionSummaryResponse {
  restrictionType: string;
  description: string;
  severity: RestrictionSeverity;
  source: string;
  dataProvenance?: AttributeProvenanceResponse | null;
}

export interface RecommendationEvidenceResponse {
  source: string;
  description: string;
  relatedCriterionName?: string | null;
  dataProvenance?: AttributeProvenanceResponse | null;
}

export interface LandParcelRecommendationResponse {
  parcelId: string;
  cadastralNumber: string;
  suitabilityScore: number;
  rank: number;
  matchingCriteria: CriterionEvaluationResponse[];
  failedCriteria: CriterionEvaluationResponse[];
  restrictions: RestrictionSummaryResponse[];
  evidence: RecommendationEvidenceResponse[];
  explanation: string;
  hardConstraintRejected: boolean;
  hardConstraintReason?: string | null;
  /** Screening status only — not legal lease availability. */
  candidateStatusLabel?: string | null;
  candidateStatusReason?: string | null;
  sourceReportedClaimantCategoryCode?: string | null;
  sourceReportedClaimantCategoryDisplay?: string | null;
  sourceParcelStatusCode?: string | null;
  sourceProvenanceLabel?: string | null;
  sourceImportedAtUtc?: string | null;
  /** Project dataset designation only — never official lease availability. */
  projectLeaseReadyLabel?: string | null;
  isProjectLeaseReadyDesignated?: boolean;
  isProjectLeaseReadyEffective?: boolean;
  parcelAreaHectares?: number | null;
  displayOrder?: number;
  ruleScoreRank?: number;
  ruleScoreTieCount?: number;
  isTiedOnRuleScore?: boolean;
  /** Official lease availability ("Not verified" / "Unknown") — separate from project designation. */
  actualLeaseAvailabilityLabel?: string | null;
}

export interface LandRecommendationSearchResultsResponse {
  requiredPurpose: LandUseType;
  recommendations: LandParcelRecommendationResponse[];
  candidateCount: number;
  generatedAt: string;
  transparency?: RecommendationSearchTransparencyResponse | null;
}

export interface ParcelIdentifierResponse {
  cadastralNumber: string;
  surveyPlanReference?: string | null;
}

export interface LandCategoryResponse {
  type: LandCategoryType;
  description?: string | null;
}

export interface LandUseResponse {
  type: LandUseType;
  description?: string | null;
}

/** Source-reported cadastral metadata for real imported parcels (no ownership or claimant fields). */
export interface ResearchSimulatedLeaseWorkflowResponse {
  researchRecordId: string;
  scenarioId: string;
  simulatedLeaseStatus: string;
  isSimulatedLeaseStatus: boolean;
  datasetUsage?: string | null;
  notice?: string | null;
}

export interface ProjectLeaseReadyDesignationResponse {
  datasetKey: string;
  researchRecordId: string;
  designationLabel: string;
  designationNotice: string;
  isDesignatedInProjectDataset: boolean;
  isEffectiveForLeaseFlow: boolean;
  excludedBecauseSourceReportedPrivate: boolean;
}

export interface LandParcelAvailabilityResponse {
  actualLeaseAvailabilityLabel: string;
  researchRecordId?: string | null;
  simulatedWorkflow?: ResearchSimulatedLeaseWorkflowResponse | null;
  projectLeaseReady?: ProjectLeaseReadyDesignationResponse | null;
}

export interface LandParcelMetricsResponse {
  totalParcelCount: number;
  projectDesignatedParcelsCount: number;
  projectDesignatedParcelsEffectiveCount: number;
}

export type GisIntelligenceCoverageStatus =
  | "Unavailable"
  | "Partial"
  | "Available";

export interface IndexedDistrictSummaryResponse {
  province: string;
  district: string;
  parcelCount: number;
}

export interface GisIntelligenceLayerCoverageResponse {
  layerKey: string;
  displayName: string;
  coverageStatus: GisIntelligenceCoverageStatus;
  referenceFeatureCount: number;
  parcelsWithEvidenceCount: number;
  provenanceSummary: string;
  limitationSummary: string;
}

export interface LandIntelligenceGisCoverageResponse {
  totalParcelCount: number;
  indexedDistricts: IndexedDistrictSummaryResponse[];
  configuredEnrichmentDistricts: string[];
  enrichmentSnapshotCount: number;
  enrichmentCompleteCount: number;
  enrichmentPartialCount: number;
  enrichmentUnavailableCount: number;
  overallEnrichmentStatus: GisIntelligenceCoverageStatus;
  overallEnrichmentNotice: string;
  intelligenceLayers: GisIntelligenceLayerCoverageResponse[];
  measuredAtUtc: string;
}

export interface ParcelGisEnrichmentSummaryResponse {
  overallStatus: GisIntelligenceCoverageStatus;
  detectedProvince?: string | null;
  detectedDistrict?: string | null;
  enrichedAtUtc?: string | null;
  assessmentNotice: string;
}

export interface ResearchDemoConfigResponse {
  enabled: boolean;
  officerLeaseInterestNicLookupEnabled: boolean;
  authorityLeaseInterestReviewEnabled?: boolean;
}

export interface GnDivisionAdministrativeAssignmentResponse {
  gnName: string;
  gnNumber?: string | null;
  divisionalSecretariat?: string | null;
}

export interface ParcelGnDivisionMappingResponse {
  status: string;
  workbookStatusLabel: string;
  reviewStatusDisplay: string;
  provenanceLabel: string;
  sourceGnIndex: string;
  primaryAssignment?: GnDivisionAdministrativeAssignmentResponse | null;
  sourceFromAdministrativeCode?: GnDivisionAdministrativeAssignmentResponse | null;
  gnAtMapMarker?: GnDivisionAdministrativeAssignmentResponse | null;
  codeDistrict?: string | null;
  mapMarkerLatitude?: number | null;
  mapMarkerLongitude?: number | null;
  datasetKey: string;
  cadastralNumber: string;
  importedAtUtc: string;
}

export interface CadastralSourceMetadataResponse {
  sourceProvenanceLabel: string;
  sourceLayer: string;
  sourceFeatureId: string;
  sourceReportedLandUseCode?: string | null;
  sourceReportedLandUseEnglishLabel?: string | null;
  sourceReportedLandUseText?: string | null;
  sourceReportedLandUseDisplay: string;
  sourceReportedClaimantCategoryCode?: string | null;
  sourceReportedClaimantCategoryEnglishLabel?: string | null;
  sourceReportedClaimantCategoryDisplay: string;
  sourceParcelStatusCode?: string | null;
  gnIndex?: string | null;
  coordinateStatus?: string | null;
  importedAtUtc: string;
  researchRecordId?: string | null;
  mapMarkerLatitude?: number | null;
  mapMarkerLongitude?: number | null;
}

export interface SourceReportedLandUseOptionResponse {
  code: string;
  englishLabel: string;
  parcelCount: number;
}

export interface LandAreaResponse {
  value: number;
  unit: AreaUnit;
}

export interface AdministrativeLocationResponse {
  province: string;
  district: string;
  divisionalSecretariat: string;
  gramaNiladhariDivision?: string | null;
}

export interface LandCharacteristicsResponse {
  soilType?: string | null;
  terrainDescription?: string | null;
  elevationMeters?: number | null;
}

/** GeoJSON Polygon (RFC 7946). Coordinates are [longitude, latitude]. */
export interface GeoJsonPolygonResponse {
  type: string;
  coordinates: number[][][];
}

export interface SpatialReferenceResponse {
  centroidLatitude: number;
  centroidLongitude: number;
  coordinateSystem: string;
  boundaryReference?: string | null;
  /** Persisted parcel boundary when available; null when only a centroid is stored. */
  boundaryPolygon?: GeoJsonPolygonResponse | null;
}

export interface LandParcelResponse {
  id: string;
  identifier: ParcelIdentifierResponse;
  category: LandCategoryResponse;
  currentUse?: LandUseResponse | null;
  area: LandAreaResponse;
  location: AdministrativeLocationResponse;
  spatial: SpatialReferenceResponse;
  characteristics?: LandCharacteristicsResponse | null;
  spatialConstraintCount: number;
  environmentalRestrictionCount: number;
  infrastructureFeatureCount: number;
  regulatoryReferenceCount: number;
  cadastralSource?: CadastralSourceMetadataResponse | null;
  availability?: LandParcelAvailabilityResponse | null;
  gisEnrichment?: ParcelGisEnrichmentSummaryResponse | null;
  gnDivisionMapping?: ParcelGnDivisionMappingResponse | null;
}

export interface SpatialConstraintResponse {
  id: string;
  landParcelId: string;
  type: SpatialConstraintType;
  description: string;
  severity: RestrictionSeverity;
}

export interface LandRelationshipResponse {
  relationshipType: string;
  sourceNodeType: string;
  sourceNodeId: string;
  targetNodeType: string;
  targetNodeId: string;
  description?: string | null;
}

export interface LandRelationshipsResponse {
  landParcelId: string;
  relationships: LandRelationshipResponse[];
}

export interface LandSearchResultResponse {
  id: string;
  cadastralNumber: string;
  province: string;
  district: string;
  categoryType: LandCategoryType;
  currentUseType: LandUseType | null;
  areaValue: number;
  areaUnit: AreaUnit;
  centroidLatitude: number;
  centroidLongitude: number;
  cadastralSource?: CadastralSourceMetadataResponse | null;
}

export interface LandSearchResultsResponse {
  results: LandSearchResultResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Project research overlay; never replaces the original cadastral land-use label. */
export interface ResearchSupplementParcelResponse {
  parcelId: string;
  recordId: string;
  supplementGroup: string;
  classificationBasis: string;
  originalLandUseLabel: string;
  gisOverlapPercent: number;
  liveVerifiedAtUtc: string;
  availabilityNotice: string;
}

/** One canonical land-use filter option; counts are distinct parcel ids (never double-counted). */
export interface CanonicalLandUseOptionResponse {
  key: string;
  label: string;
  parcelCount: number;
  sourceMatchedParcelCount: number;
  gisMatchedParcelCount: number;
  bothSourcesParcelCount: number;
}

export interface FindSuitableLandFormState {
  requiredPurpose: LandUseType | "";
  targetAreaValue: string;
  targetAreaUnit: TargetAreaInputUnit;
  preferredProvince: string;
  preferredDistrict: string;
  preferredGramaNiladhariDivision: string;
  sourceReportedLandUse: string;
  maxResults: string;
}

export const RECOMMENDATIONS_SESSION_KEY =
  "component01.landIntelligence.recommendations";

export const FIND_SUITABLE_LAND_FORM_SESSION_KEY =
  "component01.landIntelligence.findSuitableLandForm";

export interface RecommendationsSessionPayload {
  request: LandRecommendationSearchRequest;
  response: LandRecommendationSearchResultsResponse;
  submittedAt: string;
  formState?: FindSuitableLandFormState;
}

export type LeaseInterestApplicantKind = 1 | 2;

export type LeaseInterestRequestStatus =
  | 1
  | 2
  | 3
  | 4
  | 5
  | 6
  | 7;

/** Authority outcomes after officer valuation: Approved (1) or Rejected (2). */
export type LeaseInterestAuthorityDecisionKind = 1 | 2;

export interface LeaseInterestApplicant {
  kind: LeaseInterestApplicantKind;
  legalName: string;
  contactPhone: string;
  contactEmail: string;
  postalAddress: string;
  organizationName?: string | null;
  organizationRegistrationNumber?: string | null;
  nationalIdentityCardNumber?: string | null;
  representativeNationalIdentityCardNumber?: string | null;
}

export interface LeaseInterestProposal {
  requestedPurpose: LandUseType;
  plannedActivitiesDescription: string;
  requestedAreaHectares?: number | null;
  desiredStartPeriod: string;
  requestedDurationYears: number;
  projectSummary: string;
}

export interface LeaseInterestSupportingInfo {
  supportingNotes: string;
}

export interface UpsertLeaseInterestDraft {
  applicant: LeaseInterestApplicant;
  proposal: LeaseInterestProposal;
  supporting: LeaseInterestSupportingInfo;
  consentToPreliminaryAssessment: boolean;
}

export interface LeaseInterestStatusHistoryEntry {
  status: LeaseInterestRequestStatus;
  changedAtUtc: string;
  note?: string | null;
  actorKind?: number;
  actorId?: string | null;
  visibleToApplicant?: boolean;
}

export interface LeaseInterestCostLine {
  code: string;
  label: string;
  status: string;
  amount?: number | null;
  currency?: string | null;
  rateSource?: string | null;
  effectiveDate?: string | null;
  formula?: string | null;
}

export interface LeaseInterestCostSummary {
  currency: string;
  disclaimer: string;
  numericEstimateAvailable: boolean;
  lines: LeaseInterestCostLine[];
}

export interface LeaseInterestApplicantAuthorityDecision {
  decision: LeaseInterestAuthorityDecisionKind;
  applicantFacingReason: string;
  recordedAtUtc: string;
  decisionLabel: string;
  nextActionHint?: string | null;
}

export interface LeaseInterestRequestDetail {
  id: string;
  landParcelId: string;
  status: LeaseInterestRequestStatus;
  acknowledgementReference?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  submittedAtUtc?: string | null;
  applicant: LeaseInterestApplicant;
  proposal: LeaseInterestProposal;
  supporting: LeaseInterestSupportingInfo;
  consentToPreliminaryAssessment: boolean;
  statusHistory: LeaseInterestStatusHistoryEntry[];
  costSummary: LeaseInterestCostSummary;
  latestAuthorityDecision?: LeaseInterestApplicantAuthorityDecision | null;
}

export interface LeaseInterestRequestSummary {
  id: string;
  landParcelId: string;
  status: LeaseInterestRequestStatus;
  acknowledgementReference?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  submittedAtUtc?: string | null;
  requestedPurpose: LandUseType;
  requestedDurationYears?: number | null;
  cadastralNumber?: string | null;
}

export interface ParcelShortlistItem {
  landParcelId: string;
  savedAtUtc: string;
  cadastralNumber?: string | null;
  district?: string | null;
  areaHectares?: number | null;
}

export interface LeaseInterestDocumentChecklistItem {
  code: string;
  label: string;
  note: string;
}

export interface LeaseInterestDocumentChecklist {
  items: LeaseInterestDocumentChecklistItem[];
  storageNote: string;
}

export interface LeaseInterestOfficerLookupRequest {
  nationalIdentityCardNumber: string;
  page?: number;
  pageSize?: number;
}

export interface LeaseInterestOfficerLookupItem {
  requestId: string;
  landParcelId: string;
  acknowledgementReference?: string | null;
  submittedAtUtc?: string | null;
  status: LeaseInterestRequestStatus;
  cadastralNumber?: string | null;
  district?: string | null;
  requestedPurpose: LandUseType;
  requestedDurationYears?: number | null;
  nationalIdentityCardNumber: string;
}

export interface LeaseInterestOfficerLookupResponse {
  items: LeaseInterestOfficerLookupItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LeaseInterestOfficerQueueItem {
  requestId: string;
  landParcelId: string;
  acknowledgementReference?: string | null;
  submittedAtUtc?: string | null;
  status: LeaseInterestRequestStatus;
  cadastralNumber?: string | null;
  district?: string | null;
}

export interface LeaseInterestOfficerQueueResponse {
  items: LeaseInterestOfficerQueueItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LeaseInterestOfficerStatusUpdate {
  newStatus: LeaseInterestRequestStatus;
  note: string;
  visibleToApplicant?: boolean;
}

export interface LeaseInterestOfficerRequestDetail {
  id: string;
  landParcelId: string;
  status: LeaseInterestRequestStatus;
  acknowledgementReference?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  submittedAtUtc?: string | null;
  applicant: LeaseInterestApplicant;
  proposal: LeaseInterestProposal;
  supporting: LeaseInterestSupportingInfo;
  consentToPreliminaryAssessment: boolean;
  statusHistory: LeaseInterestStatusHistoryEntry[];
  cadastralNumber?: string | null;
  district?: string | null;
  applicantOutcomeSummary: string;
  latestAuthorityDecision?: LeaseInterestApplicantAuthorityDecision | null;
}

export type LeaseInterestValuationFormType = "AgriculturalCommercial" | "Residential";

export interface LeaseWorksheetPeriodLine {
  period: string;
  amountLkr?: number | null;
}

export interface LeaseWorksheetDetails {
  leaseFileNumber?: string | null;
  divisionalSecretariat?: string | null;
  leaseApprovalDate?: string | null;
  leaseCommencementDate?: string | null;
  valuationReportDate?: string | null;
  leaseAgreementDate?: string | null;
  previousAssessedValueLkr?: number | null;
  leaseRatePercent?: number | null;
  annualLeaseAmountLkr?: number | null;
  firstPeriodAmountLkr?: number | null;
  secondPeriodAmountLkr?: number | null;
  secondPeriodAnnualAmountLkr?: number | null;
  circular2018AmountLkr?: number | null;
  circular2020SixAdjustmentLkr?: number | null;
  firstPeriodLines?: LeaseWorksheetPeriodLine[] | null;
  secondPeriodLines?: LeaseWorksheetPeriodLine[] | null;
  worksheetNotes?: string | null;
}

export interface RecordLeaseInterestValuation {
  formType: LeaseInterestValuationFormType;
  assessedLandValueLkr: number;
  assessedAreaHectares?: number | null;
  inspectionDate?: string | null;
  valuationDate?: string | null;
  valuationReference?: string | null;
  issuingOffice?: string | null;
  siteCondition?: string | null;
  cultivationDetails?: string | null;
  residentialImprovements?: string | null;
  projectUseDetails?: string | null;
  otherTypeDetails?: string | null;
  officerNotes?: string | null;
  worksheet: LeaseWorksheetDetails;
}

export interface LeaseInterestValuation extends Omit<RecordLeaseInterestValuation, "worksheet" | "formType"> {
  formType: LeaseInterestValuationFormType | "Agricultural" | "Project" | "Other";
  worksheet?: LeaseWorksheetDetails | null;
  id: string;
  leaseInterestRequestId: string;
  landParcelId: string;
  revision: number;
  idempotencyKey?: string;
  recordedByOfficerId: string;
  recordedAtUtc: string;
}

/** Applicant-visible latest valuation summary (no officer notes). */
export interface LeaseInterestApplicantValuationSummary {
  revision: number;
  recordedAtUtc: string;
  formType: LeaseInterestValuationFormType | "Agricultural" | "Project" | "Other" | string;
  assessedLandValueLkr: number;
  leaseFileNumber?: string | null;
  divisionalSecretariat?: string | null;
}

export interface LeaseInterestAuthorityQueueItem {
  requestId: string;
  landParcelId: string;
  acknowledgementReference?: string | null;
  submittedAtUtc?: string | null;
  latestValuationAtUtc?: string | null;
  latestValuationRevision: number;
  status: LeaseInterestRequestStatus;
  cadastralNumber?: string | null;
  district?: string | null;
}

export interface LeaseInterestAuthorityQueueResponse {
  items: LeaseInterestAuthorityQueueItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface LeaseInterestAuthorityDecision {
  id: string;
  leaseInterestRequestId: string;
  decision: LeaseInterestAuthorityDecisionKind;
  reason: string;
  reviewedValuationId: string;
  reviewedValuationRevision: number;
  recordedByAuthorityId: string;
  recordedAtUtc: string;
  resultingStatus: LeaseInterestRequestStatus;
}

export interface RecordLeaseInterestAuthorityDecision {
  decision: LeaseInterestAuthorityDecisionKind;
  reason: string;
  expectedUpdatedAtUtc: string;
  reviewedValuationId: string;
  reviewedValuationRevision: number;
}

export interface LeaseInterestAuthorityRequestDetail {
  id: string;
  landParcelId: string;
  status: LeaseInterestRequestStatus;
  acknowledgementReference?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  submittedAtUtc?: string | null;
  applicant: LeaseInterestApplicant;
  proposal: LeaseInterestProposal;
  supporting: LeaseInterestSupportingInfo;
  consentToPreliminaryAssessment: boolean;
  statusHistory: LeaseInterestStatusHistoryEntry[];
  cadastralNumber?: string | null;
  district?: string | null;
  applicantOutcomeSummary: string;
  latestValuation?: LeaseInterestValuation | null;
  valuations: LeaseInterestValuation[];
  authorityDecisions: LeaseInterestAuthorityDecision[];
  canRecordDecision: boolean;
}
