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
  projectLeaseReadyLabel?: string | null;
  isProjectLeaseReadyDesignated?: boolean;
  isProjectLeaseReadyEffective?: boolean;
  parcelAreaHectares?: number | null;
  displayOrder?: number;
  ruleScoreRank?: number;
  ruleScoreTieCount?: number;
  isTiedOnRuleScore?: boolean;
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
  projectLeaseReadyDesignatedCount: number;
  projectLeaseReadyEffectiveCount: number;
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

export interface FindSuitableLandFormState {
  requiredPurpose: LandUseType | "";
  targetAreaValue: string;
  targetAreaUnit: TargetAreaInputUnit;
  preferredProvince: string;
  preferredDistrict: string;
  preferredDivisionalSecretariat: string;
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
  | 6;

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
  maskedNationalIdentityCardNumber: string;
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
}
