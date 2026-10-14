using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;

namespace StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures;

/// <summary>
/// Explicitly labeled research development fixture provider for Component 4 screening.
/// Provides realistic prototype case inputs matching the digital governance storyboard
/// (e.g. Eco Tourism Resort on coastal parcel PL-00123).
/// <para>
/// Notice: These fixtures are strictly development prototypes for independent C4 validation
/// and MUST NOT be presented as live Component 3 production integration.
/// </para>
/// </summary>
public static class C4DevelopmentCaseFixtureProvider
{
    private static readonly DateTimeOffset FixedBaseTime = new(2025, 5, 18, 9, 30, 0, TimeSpan.Zero);

    public static IReadOnlyList<GovernanceScreeningCaseInputDto> GetAllFixtures() => new[]
    {
        GetEcoTourismResortFixture(),
        GetSolarPowerPlantFixture(),
        GetAgricultureProjectFixture()
    };

    public static GovernanceScreeningCaseInputDto? TryGetByCaseId(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId)) return null;
        return GetAllFixtures().FirstOrDefault(f => string.Equals(f.CaseId, caseId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Storyboard Primary Case: Case LLA-2025-001
    /// Eco Tourism Resort proposal on Coastal parcel PL-00123 (Bay District / Southern Province).
    /// Expected Outcome: Recommended for Proceed with Conditions (near sensitive area, adjacent lease PL-00120).
    /// </summary>
    public static GovernanceScreeningCaseInputDto GetEcoTourismResortFixture()
    {
        return new GovernanceScreeningCaseInputDto(
            CaseId: "LLA-2025-001",
            ApplicationReference: "REF-2025-00123-ECO",
            ProposalVersion: "1.0",
            VerifiedFactSnapshotId: Guid.Parse("11111111-2222-3333-4444-555555555551"),
            SnapshotTimestampUtc: FixedBaseTime,
            ParcelId: "PARCEL-PL-00123",
            CadastralNumber: "PL-00123",
            ApplicantName: "John Doe",
            ProposedUse: "Tourism Development",
            ExtentHectares: 15.0m, // 15 hectares
            LeaseDurationYears: 30,
            InvestmentAmount: 8_000_000m, // USD 8M
            InvestmentCurrency: "USD",
            ZoningArea: "Coastal Commercial Special Zone",
            District: "Hambantota",
            DivisionalSecretariat: "Tangalle",
            Province: "Southern",
            DocumentReferences: new[]
            {
                new ScreeningDocumentReferenceDto("ProposalDocument", "Resort_Development_Proposal.pdf", "local://docs/LLA-2025-001/proposal.pdf", 1, "project_title"),
                new ScreeningDocumentReferenceDto("ProposalDocument", "Resort_Development_Proposal.pdf", "local://docs/LLA-2025-001/proposal.pdf", 2, "land_extent"),
                new ScreeningDocumentReferenceDto("ProposalDocument", "Resort_Development_Proposal.pdf", "local://docs/LLA-2025-001/proposal.pdf", 3, "proposed_land_use"),
                new ScreeningDocumentReferenceDto("ProposalDocument", "Resort_Development_Proposal.pdf", "local://docs/LLA-2025-001/proposal.pdf", 4, "investment_amount"),
                new ScreeningDocumentReferenceDto("EnvironmentalAssessment", "Environmental_Assessment.pdf", "local://docs/LLA-2025-001/eia.pdf", 1, "environmental_clearance"),
                new ScreeningDocumentReferenceDto("LocationMap", "Location_Map.pdf", "local://docs/LLA-2025-001/map.pdf", 1, "survey_coordinates")
            },
            VerifiedLegalDisputeFlag: false,
            UnauthorizedOccupationFlag: false,
            ObjectionReceivedFlag: false,
            FamilyOrInheritanceClaimFlag: true, // Potential family claim noted for review
            DocumentedPriorActivityFlag: true, // Previous temporary permit recorded
            ContemporaneousComplaints: new[]
            {
                new ScreeningComplaintEvidenceDto(
                    ComplaintId: "CMP-2025-001",
                    Category: "Environmental",
                    Summary: "Proximity to coastal bird nesting sanctuary requires environmental buffer verification.",
                    VerificationStatus: "UnderVerification",
                    ReceivedAtUtc: FixedBaseTime.AddDays(-2)
                )
            },
            FinancialAssessmentReference: "C2-FEASIBILITY-REPORT-2025-88",
            SpatialEvidence: new C1ParcelEvidenceDto(
                ParcelId: "PARCEL-PL-00123",
                CadastralNumber: "PL-00123",
                ExtentHectares: 15.0m,
                Province: "Southern",
                District: "Hambantota",
                DivisionalSecretariat: "Tangalle",
                GramaNiladhariDivision: "Tangalle West",
                CoordinateSystem: "EPSG:5235 (SLD99)",
                CentroidLatitude: 6.0245,
                CentroidLongitude: 80.7932,
                BoundaryGeoJson: "{\"type\":\"Polygon\",\"coordinates\":[[[80.791,6.023],[80.795,6.023],[80.795,6.026],[80.791,6.026],[80.791,6.023]]]}",
                SpatialConstraints: new[]
                {
                    new C1SpatialConstraintItemDto("CoastalZoneBuffer", "Medium", "Parcel lies within 100m coastal setback buffer; complies with CZM approved tourism guidelines.", 20.0m, true, 100.0),
                    new C1SpatialConstraintItemDto("WildlifeBuffer", "Medium", "Near coastal wetland sanctuary; requires baseline wildlife clearance.", 10.0m, true, 300.0)
                },
                AdjacentLeases: new[]
                {
                    new C1AdjacentLeaseEvidenceDto("LEASE-ACT-2019-08", "PL-00120", "Southern Coast Charters", "Active", 120)
                },
                HasOverlappingParcels: false,
                OverlapDetails: "No overlapping parcels detected."
            ),
            ConcurrentApplications: Array.Empty<ConcurrentApplicationEvidenceDto>()
        );
    }

    /// <summary>
    /// Case LLA-2025-002: Green Energy Ltd (Solar Power Plant, PL-00456)
    /// Clean case with no conflicts or objections.
    /// </summary>
    public static GovernanceScreeningCaseInputDto GetSolarPowerPlantFixture()
    {
        return new GovernanceScreeningCaseInputDto(
            CaseId: "LLA-2025-002",
            ApplicationReference: "REF-2025-00456-SOLAR",
            ProposalVersion: "1.0",
            VerifiedFactSnapshotId: Guid.Parse("22222222-3333-4444-5555-666666666662"),
            SnapshotTimestampUtc: FixedBaseTime.AddDays(-10),
            ParcelId: "PARCEL-PL-00456",
            CadastralNumber: "PL-00456",
            ApplicantName: "Green Energy Ltd",
            ProposedUse: "Renewable Energy",
            ExtentHectares: 25.0m,
            LeaseDurationYears: 20,
            InvestmentAmount: 12_000_000m,
            InvestmentCurrency: "USD",
            ZoningArea: "Inland Industrial / Energy",
            District: "Hambantota",
            DivisionalSecretariat: "Hambantota",
            Province: "Southern",
            DocumentReferences: new[]
            {
                new ScreeningDocumentReferenceDto("ProposalDocument", "Solar_Farm_Feasibility.pdf", "local://docs/LLA-2025-002/solar.pdf", 1, "project_title")
            },
            VerifiedLegalDisputeFlag: false,
            UnauthorizedOccupationFlag: false,
            ObjectionReceivedFlag: false,
            FamilyOrInheritanceClaimFlag: false,
            DocumentedPriorActivityFlag: false,
            ContemporaneousComplaints: Array.Empty<ScreeningComplaintEvidenceDto>(),
            FinancialAssessmentReference: "C2-FEASIBILITY-REPORT-2025-102",
            SpatialEvidence: new C1ParcelEvidenceDto(
                ParcelId: "PARCEL-PL-00456",
                CadastralNumber: "PL-00456",
                ExtentHectares: 25.0m,
                Province: "Southern",
                District: "Hambantota",
                DivisionalSecretariat: "Hambantota",
                GramaNiladhariDivision: "Mirijjawila",
                CoordinateSystem: "EPSG:5235 (SLD99)",
                CentroidLatitude: 6.1340,
                CentroidLongitude: 81.1120,
                BoundaryGeoJson: null,
                SpatialConstraints: Array.Empty<C1SpatialConstraintItemDto>(),
                AdjacentLeases: Array.Empty<C1AdjacentLeaseEvidenceDto>(),
                HasOverlappingParcels: false,
                OverlapDetails: null
            ),
            ConcurrentApplications: Array.Empty<ConcurrentApplicationEvidenceDto>()
        );
    }

    /// <summary>
    /// Case LLA-2025-003: Sunrise Co (Agriculture Project, PL-00789)
    /// High-conflict case: Active boundary dispute and multiple complaints.
    /// </summary>
    public static GovernanceScreeningCaseInputDto GetAgricultureProjectFixture()
    {
        return new GovernanceScreeningCaseInputDto(
            CaseId: "LLA-2025-003",
            ApplicationReference: "REF-2025-00789-AGRI",
            ProposalVersion: "1.0",
            VerifiedFactSnapshotId: Guid.Parse("33333333-4444-5555-6666-777777777773"),
            SnapshotTimestampUtc: FixedBaseTime.AddDays(-20),
            ParcelId: "PARCEL-PL-00789",
            CadastralNumber: "PL-00789",
            ApplicantName: "Sunrise Co",
            ProposedUse: "Agriculture Project",
            ExtentHectares: 50.0m,
            LeaseDurationYears: 30,
            InvestmentAmount: 3_500_000m,
            InvestmentCurrency: "LKR",
            ZoningArea: "Agricultural Reserve",
            District: "Hambantota",
            DivisionalSecretariat: "Ambalantota",
            Province: "Southern",
            DocumentReferences: new[]
            {
                new ScreeningDocumentReferenceDto("ProposalDocument", "Organic_Farm_Proposal.pdf", "local://docs/LLA-2025-003/agri.pdf", 1, "project_title")
            },
            VerifiedLegalDisputeFlag: true, // Verified legal dispute
            UnauthorizedOccupationFlag: true, // Unauthorized occupation reported
            ObjectionReceivedFlag: true, // Formal objection received
            FamilyOrInheritanceClaimFlag: false,
            DocumentedPriorActivityFlag: true,
            ContemporaneousComplaints: new[]
            {
                new ScreeningComplaintEvidenceDto(
                    ComplaintId: "CMP-2025-002",
                    Category: "Boundary Dispute",
                    Summary: "Encroachment on adjacent village grazing land under dispute in District Court.",
                    VerificationStatus: "Verified",
                    ReceivedAtUtc: FixedBaseTime.AddDays(-15)
                )
            },
            FinancialAssessmentReference: null,
            SpatialEvidence: new C1ParcelEvidenceDto(
                ParcelId: "PARCEL-PL-00789",
                CadastralNumber: "PL-00789",
                ExtentHectares: 50.0m,
                Province: "Southern",
                District: "Hambantota",
                DivisionalSecretariat: "Ambalantota",
                GramaNiladhariDivision: "Ridiyagama",
                CoordinateSystem: "EPSG:5235 (SLD99)",
                CentroidLatitude: 6.2100,
                CentroidLongitude: 81.0100,
                BoundaryGeoJson: null,
                SpatialConstraints: new[]
                {
                    new C1SpatialConstraintItemDto("AgriculturalReserveProtection", "High", "Designated agricultural reserve subject to strict land development covenants.", 100.0m, false, null)
                },
                AdjacentLeases: Array.Empty<C1AdjacentLeaseEvidenceDto>(),
                HasOverlappingParcels: true,
                OverlapDetails: "Potential 2.4 ha overlap with traditional grazing boundary."
            ),
            ConcurrentApplications: new[]
            {
                new ConcurrentApplicationEvidenceDto("LLA-2025-009", "Southern Agro Collective", "Organic Farming", FixedBaseTime.AddDays(-22), "UnderReview")
            }
        );
    }
}

