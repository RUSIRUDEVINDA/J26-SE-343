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

export interface AccessibilityCriteria {
  maxRoadDistanceMeters?: number | null;
  requireRoadAccess: boolean;
}

export interface EnvironmentalCriteria {
  maxAllowedEnvironmentalSeverity: RestrictionSeverity;
  rejectProhibitiveEnvironmentalRestrictions: boolean;
}

export interface LandRecommendationSearchRequest {
  requiredPurpose: LandUseType;
  requiredAreaHectares?: number | null;
  areaTolerancePercent?: number;
  preferredLocation?: PreferredLocationCriteria | null;
  requiredLandCategory?: LandCategoryType | null;
  accessibility?: AccessibilityCriteria | null;
  environmental?: EnvironmentalCriteria | null;
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
}

export interface LandRecommendationSearchResultsResponse {
  requiredPurpose: LandUseType;
  recommendations: LandParcelRecommendationResponse[];
  candidateCount: number;
  generatedAt: string;
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
}

export interface LandSearchResultsResponse {
  results: LandSearchResultResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface FindSuitableLandFormState {
  requiredPurpose: LandUseType | "";
  requiredAreaHectares: string;
  preferredProvince: string;
  preferredDistrict: string;
  preferredDivisionalSecretariat: string;
  requiredLandCategory: LandCategoryType | "";
  maxRoadDistanceMeters: string;
  requireRoadAccess: boolean;
  environmentalPreference: string;
  rejectProhibitiveEnvironmentalRestrictions: boolean;
  maxResults: string;
  waterProximityPreference: string;
  soilGroupPreference: string;
  additionalRequirements: string;
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
