import {
  AreaUnit,
  CriterionCategory,
  LandCategoryType,
  LandUseType,
  RestrictionSeverity,
  SpatialConstraintType,
  supportedLandUseTypes,
} from "../types/landIntelligence";

export const landUseTypeLabels: Record<LandUseType, string> = {
  [LandUseType.Agricultural]: "Agricultural",
  [LandUseType.Commercial]: "Commercial",
  [LandUseType.Industrial]: "Industrial",
  [LandUseType.Residential]: "Residential",
  [LandUseType.Tourism]: "Tourism",
  [LandUseType.Conservation]: "Conservation",
  [LandUseType.MixedUse]: "Mixed use",
  [LandUseType.Other]: "Other",
};

export const landCategoryTypeLabels: Record<LandCategoryType, string> = {
  [LandCategoryType.StateLand]: "State land",
  [LandCategoryType.CrownLand]: "Crown land",
  [LandCategoryType.ReservedLand]: "Reserved land",
  [LandCategoryType.Other]: "Other",
};

export const restrictionSeverityLabels: Record<RestrictionSeverity, string> = {
  [RestrictionSeverity.Low]: "Low",
  [RestrictionSeverity.Medium]: "Medium",
  [RestrictionSeverity.High]: "High",
  [RestrictionSeverity.Prohibitive]: "Prohibitive",
};

export const spatialConstraintTypeLabels: Record<SpatialConstraintType, string> = {
  [SpatialConstraintType.Zoning]: "Zoning",
  [SpatialConstraintType.BufferZone]: "Buffer zone",
  [SpatialConstraintType.Setback]: "Setback",
  [SpatialConstraintType.LandUsePlan]: "Land use plan",
  [SpatialConstraintType.Other]: "Other",
};

export const areaUnitLabels: Record<AreaUnit, string> = {
  [AreaUnit.SquareMeters]: "m²",
  [AreaUnit.Hectares]: "ha",
  [AreaUnit.Acres]: "ac",
};

export const criterionCategoryLabels: Record<CriterionCategory, string> = {
  [CriterionCategory.SoilSuitability]: "Soil suitability",
  [CriterionCategory.InfrastructureAccess]: "Infrastructure access",
  [CriterionCategory.EnvironmentalCompatibility]: "Environmental compatibility",
  [CriterionCategory.ZoningCompliance]: "Zoning compliance",
  [CriterionCategory.EconomicPotential]: "Economic potential",
  [CriterionCategory.HistoricalPerformance]: "Historical performance",
  [CriterionCategory.RequiredArea]: "Required area",
  [CriterionCategory.LandCategoryMatch]: "Land category match",
  [CriterionCategory.LandUseMatch]: "Land use match",
  [CriterionCategory.LocationPreference]: "Location preference",
  [CriterionCategory.AccessibilityRequirement]: "Accessibility requirement",
  [CriterionCategory.EnvironmentalRequirement]: "Environmental requirement",
  [CriterionCategory.RegulatoryRequirement]: "Regulatory requirement",
  [CriterionCategory.SpatialConstraintImpact]: "Spatial constraint impact",
  [CriterionCategory.CustomCriterion]: "Custom criterion",
};

export const landUseTypeOptions = supportedLandUseTypes.map((value) => ({
  value,
  label: landUseTypeLabels[value],
}));

export const landCategoryOptions = [
  { value: "", label: "Any category" },
  ...Object.entries(landCategoryTypeLabels).map(([value, label]) => ({
    value: Number(value) as LandCategoryType,
    label,
  })),
];
