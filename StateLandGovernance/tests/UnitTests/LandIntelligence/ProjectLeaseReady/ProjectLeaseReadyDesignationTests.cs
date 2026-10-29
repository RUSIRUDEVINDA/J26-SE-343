using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Application.Services;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;
using StateLandGovernance.UnitTests.LandIntelligence.Recommendations;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.UnitTests.LandIntelligence.ProjectLeaseReady;

public sealed class ProjectLeaseReadyDesignationTests
{
    [Fact]
    public void Evaluate_StateParcel_IsEffectiveWithProjectLabel()
    {
        var parcelId = Guid.NewGuid();
        var designation = CreateDesignation(parcelId, "COL-STATE-001");
        var source = CreateSource("2", "State");

        var evaluation = ProjectLeaseReadyDesignationEvaluator.Evaluate(designation, source);

        Assert.True(evaluation.IsDesignatedInProjectDataset);
        Assert.True(evaluation.IsEffectiveForLeaseFlow);
        Assert.Equal(ColomboStateResearchProjectLeaseReady.DesignationLabel, evaluation.ProjectDesignationLabel);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, evaluation.DisplayLeaseAvailabilityLabel);
        Assert.False(evaluation.Designation.ExcludedBecauseSourceReportedPrivate);
    }

    [Fact]
    public void Evaluate_PrivateClaimant_IsDesignatedButNotEffective()
    {
        var parcelId = Guid.NewGuid();
        var designation = CreateDesignation(parcelId, "COL-STATE-PRIVATE");
        var source = CreateSource("3", "Private");

        var evaluation = ProjectLeaseReadyDesignationEvaluator.Evaluate(designation, source);

        Assert.True(evaluation.IsDesignatedInProjectDataset);
        Assert.False(evaluation.IsEffectiveForLeaseFlow);
        Assert.Equal(ProjectLeaseReadyDesignationEvaluator.PrivateExclusionLeaseLabel, evaluation.ProjectDesignationLabel);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, evaluation.DisplayLeaseAvailabilityLabel);
        Assert.True(evaluation.Designation.ExcludedBecauseSourceReportedPrivate);
    }

    [Fact]
    public void Evaluate_MissingClaimant_IsEffectiveWithProjectLabelAndNotVerifiedAvailability()
    {
        var designation = CreateDesignation(Guid.NewGuid(), "COL-STATE-NO-CLAIMANT");

        var withoutSource = ProjectLeaseReadyDesignationEvaluator.Evaluate(designation, null);
        var withoutClaimantCode = ProjectLeaseReadyDesignationEvaluator.Evaluate(
            designation,
            CreateSource(null, null));

        foreach (var evaluation in new[] { withoutSource, withoutClaimantCode })
        {
            Assert.True(evaluation.IsDesignatedInProjectDataset);
            Assert.True(evaluation.IsEffectiveForLeaseFlow);
            Assert.Equal(ColomboStateResearchProjectLeaseReady.DesignationLabel, evaluation.ProjectDesignationLabel);
            Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, evaluation.DisplayLeaseAvailabilityLabel);
        }
    }

    [Fact]
    public void Build_WithoutDesignation_KeepsUnknownOrNotVerified()
    {
        var withSource = LandParcelAvailabilityMapper.Build(CreateSource("2", "State"), null, false);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, withSource.ActualLeaseAvailabilityLabel);
        Assert.Null(withSource.ProjectLeaseReady);

        var withoutSource = LandParcelAvailabilityMapper.Build(null, null, false);
        Assert.Equal("Unknown", withoutSource.ActualLeaseAvailabilityLabel);
        Assert.Null(withoutSource.ProjectLeaseReady);
    }

    [Fact]
    public async Task Composer_Metrics_ExcludesPrivateFromEffectiveCount()
    {
        var stateId = Guid.NewGuid();
        var privateId = Guid.NewGuid();
        var designations = new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>
        {
            [stateId] = CreateDesignation(stateId, "R-STATE"),
            [privateId] = CreateDesignation(privateId, "R-PRIVATE"),
        };
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [stateId] = CreateSource("2", "State"),
            [privateId] = CreateSource("3", "Private"),
        };

        var composer = new LandParcelAvailabilityComposer(
            new StubProjectLeaseReadyRepo(designations, totalParcels: 350),
            new StubSimulatedLeaseRepo(),
            new StubResearchDemoMode(false),
            new FakeRealParcelSourceReadRepository(sources));

        var metrics = await composer.BuildMetricsAsync();

        Assert.Equal(350, metrics.TotalParcelCount);
        Assert.Equal(2, metrics.ProjectDesignatedParcelsCount);
        Assert.Equal(1, metrics.ProjectDesignatedParcelsEffectiveCount);
    }

    [Fact]
    public async Task Composer_EnsureLeaseInterest_BlocksPrivateInProjectDataset()
    {
        var parcelId = Guid.NewGuid();
        var designations = new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>
        {
            [parcelId] = CreateDesignation(parcelId, "R-PRIVATE"),
        };
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [parcelId] = CreateSource("3", "Private"),
        };

        var composer = new LandParcelAvailabilityComposer(
            new StubProjectLeaseReadyRepo(designations),
            new StubSimulatedLeaseRepo(),
            new StubResearchDemoMode(false),
            new FakeRealParcelSourceReadRepository(sources));

        await Assert.ThrowsAsync<ValidationException>(() =>
            composer.EnsureLeaseInterestPermittedAsync(parcelId));
    }

    [Fact]
    public async Task Composer_EnsureLeaseInterest_AllowsOutsideDataset()
    {
        var parcelId = Guid.NewGuid();
        var composer = new LandParcelAvailabilityComposer(
            new StubProjectLeaseReadyRepo(new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>()),
            new StubSimulatedLeaseRepo(),
            new StubResearchDemoMode(false),
            new FakeRealParcelSourceReadRepository());

        await composer.EnsureLeaseInterestPermittedAsync(parcelId);
    }

    [Fact]
    public async Task Engine_ProjectLeaseReady_SurfacesLabelWithoutOverridingHardRestrictions()
    {
        var designated = SyntheticRecommendationParcelFactory.CreateSuitableParcel("LEASE-READY-001");
        var restricted = SyntheticRecommendationParcelFactory.CreateRestrictedParcel("LEASE-READY-RESTRICTED");

        var designations = new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>
        {
            [designated.Id] = CreateDesignation(designated.Id, "R-OK"),
            [restricted.Id] = CreateDesignation(restricted.Id, "R-BLOCKED"),
        };
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [designated.Id] = CreateSource("2", "State"),
            [restricted.Id] = CreateSource("2", "State"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(sources, designations, designated, restricted);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10,
        });

        Assert.Equal(2, response.Recommendations.Count);

        var ok = response.Recommendations.Single(r => r.CadastralNumber == "LEASE-READY-001");
        Assert.True(ok.IsProjectLeaseReadyDesignated);
        Assert.True(ok.IsProjectLeaseReadyEffective);
        Assert.Equal(ColomboStateResearchProjectLeaseReady.DesignationLabel, ok.ProjectLeaseReadyLabel);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, ok.ActualLeaseAvailabilityLabel);
        Assert.NotEqual(ok.ProjectLeaseReadyLabel, ok.ActualLeaseAvailabilityLabel);
        var designationEvidence = Assert.Single(
            ok.Evidence,
            e => e.Source == "ProjectLeaseReadyDesignation");
        Assert.Contains(ColomboStateResearchProjectLeaseReady.DesignationLabel, designationEvidence.Description);
        Assert.Contains("Official lease availability: Not verified", designationEvidence.Description);
        Assert.False(ok.HardConstraintRejected);

        var blocked = response.Recommendations.Single(r => r.CadastralNumber == "LEASE-READY-RESTRICTED");
        Assert.True(blocked.IsProjectLeaseReadyDesignated);
        Assert.True(blocked.IsProjectLeaseReadyEffective);
        Assert.True(blocked.HardConstraintRejected);
        Assert.False(string.IsNullOrWhiteSpace(blocked.HardConstraintReason));
    }

    [Fact]
    public async Task Engine_OutsideDataset_HasNoProjectDesignationButNotVerifiedAvailability()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("OUTSIDE-DATASET-001");
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [parcel.Id] = CreateSource("2", "State"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(
            sources,
            new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>(),
            parcel);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10,
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.False(recommendation.IsProjectLeaseReadyDesignated);
        Assert.False(recommendation.IsProjectLeaseReadyEffective);
        Assert.Null(recommendation.ProjectLeaseReadyLabel);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, recommendation.ActualLeaseAvailabilityLabel);
        Assert.DoesNotContain(recommendation.Evidence, e => e.Source == "ProjectLeaseReadyDesignation");
    }

    [Fact]
    public async Task Engine_DesignatedWithoutClaimantSource_KeepsProjectLabelAndNotVerifiedAvailability()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("MISSING-CLAIMANT-001");
        var designations = new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>
        {
            [parcel.Id] = CreateDesignation(parcel.Id, "R-NO-SOURCE"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(
            new Dictionary<Guid, CadastralSourceMetadataDto>(),
            designations,
            parcel);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10,
        });

        var recommendation = Assert.Single(response.Recommendations);
        Assert.True(recommendation.IsProjectLeaseReadyDesignated);
        Assert.True(recommendation.IsProjectLeaseReadyEffective);
        Assert.Equal(ColomboStateResearchProjectLeaseReady.DesignationLabel, recommendation.ProjectLeaseReadyLabel);
        Assert.Equal(LandParcelAvailabilityMapper.NotVerifiedLabel, recommendation.ActualLeaseAvailabilityLabel);
    }

    [Fact]
    public async Task Engine_SourceReportedPrivate_IsExcludedFromRecommendations()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("PRIVATE-EXCLUDED-001");
        var designations = new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>
        {
            [parcel.Id] = CreateDesignation(parcel.Id, "R-PRIVATE"),
        };
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [parcel.Id] = CreateSource("3", "Private"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(sources, designations, parcel);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10,
        });

        Assert.Empty(response.Recommendations);
    }

    [Fact]
    public void UpsertSql_IsIdempotentOnResearchRecordId()
    {
        Assert.Contains("ON CONFLICT", ColomboStateResearch300ProjectLeaseReadyPersistence.UpsertSql, StringComparison.Ordinal);
        Assert.Contains("\"ResearchRecordId\"", ColomboStateResearch300ProjectLeaseReadyPersistence.UpsertSql, StringComparison.Ordinal);
    }

    // Requires StateLandGovernance/data/gis/.../state_research_300 (gitignored). Excluded from CI via Category!=Integration.
    [Fact]
    [Trait("Category", "Integration")]
    public void StateResearchCsv_HasThreeHundredDistinctRecordIdsForDesignationScope()
    {
        var root = ResolveDatasetRoot();
        var csvPath = Path.Combine(root, "colombo_state_parcels_300_real.csv");
        var rows = ColomboStateResearch300CsvParser.Parse(File.ReadAllText(csvPath));
        var columnIndex = ColomboStateResearch300CsvParser.ReadHeader(rows[0]);
        var recordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var privateCount = 0;

        foreach (var row in rows.Skip(1))
        {
            var recordId = ColomboStateResearch300CsvParser.GetField(row, columnIndex, "record_id");
            Assert.False(string.IsNullOrWhiteSpace(recordId));
            recordIds.Add(recordId);

            var claimantCode = ColomboStateResearch300CsvParser.GetField(
                row,
                columnIndex,
                "source_claimant_category_code");
            if (string.Equals(claimantCode, "3", StringComparison.Ordinal))
            {
                privateCount++;
            }
        }

        Assert.Equal(ColomboStateResearchProjectLeaseReady.ExpectedDesignationCount, recordIds.Count);
        Assert.Equal(ColomboStateResearchProjectLeaseReady.ExpectedDesignationCount - privateCount, recordIds.Count - privateCount);
    }

    // Requires StateLandGovernance/data/gis/.../state_research_300 (gitignored). Excluded from CI via Category!=Integration.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ImportDryRun_ValidatesThreeHundredParcels_ForLeaseReadyImportScope()
    {
        var root = ResolveDatasetRoot();
        var service = new ColomboStateResearch300ImportService(NullLogger<ColomboStateResearch300ImportService>.Instance);
        var report = await service.RunAsync(new ColomboStateResearch300ImportOptions(
            Path.Combine(root, "colombo_state_parcels_300_real.csv"),
            Path.Combine(root, "colombo_state_parcels_300_boundaries.geojson"),
            Path.Combine(root, "lease_workflow_SIMULATED_300.csv"),
            Apply: false,
            ReportPath: Path.Combine(Path.GetTempPath(), $"colombo-research-lease-ready-{Guid.NewGuid():N}.json")));

        Assert.Equal(300, report.ValidParcels);
        Assert.Equal(ColomboStateResearchProjectLeaseReady.ExpectedDesignationCount, report.ValidParcels);
    }

    private static ProjectLeaseReadyDesignationRecord CreateDesignation(Guid parcelId, string recordId) =>
        new(recordId, parcelId, ColomboStateResearchProjectLeaseReady.DatasetKey);

    private static CadastralSourceMetadataDto CreateSource(string? code, string? label) =>
        new(
            ColomboCadastreSourceDomains.SourceProvenanceLabel,
            "Cadastre_Parcels_Landuse_classified___Land_Use_0",
            "feature-1",
            SourceReportedLandUseCode: "73",
            SourceReportedLandUseEnglishLabel: "Paddy Field",
            SourceReportedLandUseText: null,
            SourceReportedClaimantCategoryCode: code,
            SourceReportedClaimantCategoryEnglishLabel: label,
            SourceParcelStatusCode: null,
            GnIndex: null,
            CoordinateStatus: null,
            ImportedAtUtc: DateTimeOffset.UtcNow);

    private static string ResolveDatasetRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "data",
                "gis",
                "experiments",
                "colombo",
                "state_research_300");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate state_research_300 dataset folder.");
    }

    private sealed class StubProjectLeaseReadyRepo : IProjectLeaseReadyDesignationReadRepository
    {
        private readonly IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord> _byParcel;
        private readonly int _totalParcels;

        public StubProjectLeaseReadyRepo(
            IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord> byParcel,
            int totalParcels = 0)
        {
            _byParcel = byParcel;
            _totalParcels = totalParcels > 0 ? totalParcels : byParcel.Count;
        }

        public Task<ProjectLeaseReadyDesignationRecord?> GetByParcelIdAsync(
            Guid landParcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_byParcel.TryGetValue(landParcelId, out var row) ? row : null);

        public Task<IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord>> GetByParcelIdsAsync(
            IReadOnlyList<Guid> landParcelIds,
            CancellationToken cancellationToken = default)
        {
            var result = landParcelIds
                .Where(_byParcel.ContainsKey)
                .ToDictionary(id => id, id => _byParcel[id]);
            return Task.FromResult<IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord>>(result);
        }

        public Task<IReadOnlyList<ProjectLeaseReadyDesignationRecord>> ListDesignatedAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectLeaseReadyDesignationRecord>>(_byParcel.Values.ToList());

        public Task<int> CountDesignatedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_byParcel.Count);

        public Task<int> CountTotalParcelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_totalParcels);
    }

    private sealed class StubSimulatedLeaseRepo : IResearchSimulatedLeaseReadRepository
    {
        public Task<ResearchSimulatedLeaseWorkflowDto?> GetByParcelIdAsync(
            Guid landParcelId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ResearchSimulatedLeaseWorkflowDto?>(null);
    }

    private sealed class StubResearchDemoMode(bool enabled) : IResearchDemoMode
    {
        public bool IsEnabled => enabled;
    }

}
