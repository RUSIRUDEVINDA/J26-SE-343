"""Canonical field catalog and classification taxonomy for governed semantic extraction."""

from dataclasses import dataclass
from typing import Dict, List, Optional, Set


@dataclass(frozen=True)
class FieldDefinition:
    """Metadata for a canonically supported extraction field."""

    code: str
    display_name: str
    value_kind: str  # Matches AnalysisFactValueKind: Text, Identifier, Integer, Decimal, Date, Boolean
    description: str
    is_critical: bool = False


# Canonical 17 proposal extraction fields
CANONICAL_FIELDS: Dict[str, FieldDefinition] = {
    "applicant_name": FieldDefinition(
        code="applicant_name",
        display_name="Applicant Name",
        value_kind="Text",
        description="Full legal name of the applicant individual or corporate entity.",
        is_critical=True,
    ),
    "applicant_type": FieldDefinition(
        code="applicant_type",
        display_name="Applicant Type",
        value_kind="Text",
        description="Legal nature of applicant (e.g. Individual, Private Limited Company, Cooperative).",
        is_critical=False,
    ),
    "project_title": FieldDefinition(
        code="project_title",
        display_name="Project Title",
        value_kind="Text",
        description="Official title or designation of the proposed project.",
        is_critical=True,
    ),
    "project_purpose": FieldDefinition(
        code="project_purpose",
        display_name="Project Purpose",
        value_kind="Text",
        description="Primary commercial, agricultural, or institutional purpose of the lease proposal.",
        is_critical=False,
    ),
    "land_location": FieldDefinition(
        code="land_location",
        display_name="Land Location",
        value_kind="Text",
        description="Specific village, locality, town, or address where the subject parcel is situated.",
        is_critical=True,
    ),
    "district": FieldDefinition(
        code="district",
        display_name="District",
        value_kind="Text",
        description="Administrative district in Sri Lanka (e.g., Hambantota, Kandy, Colombo, Anuradhapura).",
        is_critical=True,
    ),
    "divisional_secretariat_division": FieldDefinition(
        code="divisional_secretariat_division",
        display_name="Divisional Secretariat Division",
        value_kind="Text",
        description="Divisional Secretariat (DS) division jurisdiction administering the land.",
        is_critical=True,
    ),
    "land_extent": FieldDefinition(
        code="land_extent",
        display_name="Land Extent",
        value_kind="Text",
        description="Magnitude or quantity of land requested (e.g. '5.5', '10', '2A 1R 10P').",
        is_critical=True,
    ),
    "land_extent_unit": FieldDefinition(
        code="land_extent_unit",
        display_name="Land Extent Unit",
        value_kind="Text",
        description="Unit of land extent (e.g., Hectare, Acre, Perch, Rood).",
        is_critical=False,
    ),
    "requested_lease_period": FieldDefinition(
        code="requested_lease_period",
        display_name="Requested Lease Period",
        value_kind="Text",
        description="Requested lease duration stated in the proposal (e.g., '30 years', '50 years').",
        is_critical=True,
    ),
    "proposed_land_use": FieldDefinition(
        code="proposed_land_use",
        display_name="Proposed Land Use",
        value_kind="Text",
        description="Category of proposed land utilization (e.g., Agricultural, Industrial, Commercial, Tourism).",
        is_critical=True,
    ),
    "investment_amount": FieldDefinition(
        code="investment_amount",
        display_name="Investment Amount",
        value_kind="Text",
        description="Total estimated project capital investment as stated in the proposal.",
        is_critical=False,
    ),
    "investment_currency": FieldDefinition(
        code="investment_currency",
        display_name="Investment Currency",
        value_kind="Text",
        description="Currency denomination of the investment (e.g., LKR, USD).",
        is_critical=False,
    ),
    "expected_employment": FieldDefinition(
        code="expected_employment",
        display_name="Expected Employment",
        value_kind="Text",
        description="Projected direct or indirect employment positions generated.",
        is_critical=False,
    ),
    "project_description": FieldDefinition(
        code="project_description",
        display_name="Project Description",
        value_kind="Text",
        description="Narrative overview of proposed activities, technical operations, and infrastructure.",
        is_critical=False,
    ),
    "supporting_document_references": FieldDefinition(
        code="supporting_document_references",
        display_name="Supporting Document References",
        value_kind="Text",
        description="Explicit mentions or lists of accompanying survey plans, deeds, clearances, or certificates.",
        is_critical=False,
    ),
    "legal_statutory_basis": FieldDefinition(
        code="legal_statutory_basis",
        display_name="Legal / Statutory Basis",
        value_kind="Text",
        description="Explicit legislative or statutory enactment under which the lease is sought (e.g. State Lands Ordinance, Land Reform Act).",
        is_critical=True,
    ),
}


# Critical fields for evaluation and governance assessment
CRITICAL_FIELD_CODES: Set[str] = {
    code for code, f in CANONICAL_FIELDS.items() if f.is_critical
}

# Authoritative document classification taxonomy matching WorkflowGovernance domain categories
DOCUMENT_CLASSIFICATION_TAXONOMY: Set[str] = {
    "LeaseProposal",
    "CadastralSurveyPlan",
    "SurveyReport",
    "AffidavitOfNoEncumbrance",
    "TitleDeed",
    "EnvironmentalClearance",
    "BusinessRegistration",
    "IdentityDocument",
    "ValuationReport",
    "Other",
    "Unknown",
}

