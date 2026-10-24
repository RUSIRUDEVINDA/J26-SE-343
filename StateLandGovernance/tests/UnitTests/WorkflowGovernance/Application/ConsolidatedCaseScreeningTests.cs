namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;

public sealed class ConsolidatedCaseScreeningTests
{
    [Fact]
    public async Task ApplicantTracking_SupersededAnalysisSnapshot_DoesNotClaimCurrentVerification()
    {
        var f = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        var cases = new InMemoryLeaseCaseRepository(); var documents = new InMemoryGovernedDocumentRepository(); var analyses = new InMemoryDocumentAnalysisRepository();
        await cases.AddAsync(f.LeaseCase); await documents.AddAsync(f.Document); await analyses.AddAsync(f.Analysis);
        f.Analysis.RequestRun(new(Guid.NewGuid()), new("EXPLICIT_TEST", "Reanalysis", "2"), [new("TEST")], DateTime.UtcNow);
        var query = new StateLandGovernance.WorkflowGovernance.Application.Queries.GetApplicantCaseTrackingQueryHandler(cases, documents, analyses, new InMemoryCorrectionRepository());
        var state = await query.HandleAsync(new(f.LeaseCase.ApplicationReference, f.LeaseCase.CreatedByActorId));
        Assert.NotEqual("Verified", state!.Documents.Single().ProcessingStatus);
        Assert.NotEqual("Completed", state.Milestones.Single(m => m.MilestoneKey == "OFFICER_VERIFICATION").State);
    }

    [Fact]
    public async Task LegacySnapshotLinkHandler_CannotLinkAnotherCasesPublishedSnapshot()
    {
        var services = new ServiceCollection(); services.AddWorkflowGovernanceApplication(); services.AddWorkflowGovernanceInfrastructure(true);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope(); var sp = scope.ServiceProvider;
        var source = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(applicationReference: "APP-SCR-SOURCE-001");
        var target = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(applicationReference: "APP-SCR-TARGET-001");
        var cases = sp.GetRequiredService<ILeaseCaseRepository>();
        await cases.AddAsync(source.LeaseCase); await cases.AddAsync(target.LeaseCase);
        await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(source.Document);
        await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(source.Analysis);
        var before = target.LeaseCase.CurrentVerifiedFactSnapshotId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<LinkVerifiedFactSnapshotToCaseCommandHandler>().HandleAsync(
            new(target.LeaseCase.Id.Value, source.Analysis.Id.Value, source.Snapshot.Id.Value,
                ExpectedLeaseCaseRevision: target.LeaseCase.Revision, ExpectedAnalysisRevision: source.Analysis.Revision)));
        Assert.Equal(before, target.LeaseCase.CurrentVerifiedFactSnapshotId);
    }

    [Fact]
    public async Task HumanEvidenceLink_PreservesTheDifferentAnalysisAndCaseRevisionContracts()
    {
        var services = new ServiceCollection(); services.AddWorkflowGovernanceApplication(); services.AddWorkflowGovernanceInfrastructure(true);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope(); var sp = scope.ServiceProvider;
        var f = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(); var now = DateTime.UtcNow;
        var actor = f.LeaseCase.CreatedByActorId;
        var caseAuthority = new VerifiedAuthorityContext(actor, ["DocumentSubmitter"], "LeaseCase", f.LeaseCase.Id.Value.ToString("D"), now.AddHours(-1), now.AddMinutes(-1), now.AddHours(1));
        var proposal = new StateLandGovernance.WorkflowGovernance.Domain.Documents.GovernedDocument(f.Document.Id, f.LeaseCase.Id, "Proposal", f.Document.ActiveVersionId,
            f.Document.Versions.Single().Checksum, new("TEST_ONLY/proposal.pdf"), "TEST_ONLY.pdf", "application/pdf", 1, actor, now, caseAuthority.ToDomain());
        await sp.GetRequiredService<ILeaseCaseRepository>().AddAsync(f.LeaseCase);
        await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(proposal);
        await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(f.Analysis);
        var docAuthority = caseAuthority with { ScopeKind = "GovernedDocument", ScopeTargetIdentifier = proposal.Id.Value.ToString("D"),
            Capabilities = ["HumanEvidenceReviewer", "FactSnapshotPublisher"] };
        var human = new StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.HumanEvidenceFact(Guid.NewGuid(), proposal.Id, proposal.ActiveVersionId,
            f.Analysis.DocumentChecksum, new("requested_lease_period"), new(StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.AnalysisFactValueKind.Text, "TEST ONLY 30 years"),
            1, "TEST ONLY source quote", "TEST ONLY human review", actor, now, docAuthority.ToDomain());
        var snapshotId = new StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.VerifiedFactSnapshotId(Guid.NewGuid());
        f.Analysis.PublishHumanEvidenceSnapshot(f.Snapshot.Id, snapshotId, [human], f.Analysis.Revision, actor, now, docAuthority.ToDomain());
        Assert.NotEqual(f.Analysis.Revision, f.LeaseCase.Revision);
        await sp.GetRequiredService<HumanEvidenceFactService>().LinkAsync(f.LeaseCase.Id.Value, snapshotId.Value,
            new(proposal.Id.Value, proposal.ActiveVersionId.Value, f.Analysis.Revision, f.LeaseCase.Revision), docAuthority, default);
        Assert.Equal(snapshotId.Value, f.LeaseCase.CurrentVerifiedFactSnapshotId);
    }

    [Fact]
    public async Task ScreeningReference_RetrievesFullEnvelopeBeyondDomainRemarksLimit()
    {
        var services = new ServiceCollection(); services.AddWorkflowGovernanceApplication(); services.AddWorkflowGovernanceInfrastructure(true);
        services.AddSingleton<IConsolidatedCaseScreeningAdapter, NoDispatchAdapter>();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope(); var sp = scope.ServiceProvider;
        var fixture = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        await sp.GetRequiredService<ILeaseCaseRepository>().AddAsync(fixture.LeaseCase);
        await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(fixture.Document);
        await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(fixture.Analysis);
        var storedParcel = Guid.NewGuid();
        var full = new StoredCaseScreening("c3-c4-v1", fixture.LeaseCase.Id.Value, fixture.Snapshot.Id.Value, storedParcel, "TEST", "TEST",
            JsonSerializer.SerializeToElement(new { assessmentId = Guid.NewGuid(), findings = new string('x', 12000) }), "TEST_INPUT", "TEST_BINDINGS");
        var recordId = Guid.NewGuid();
        await sp.GetRequiredService<ICorrectionRepository>().SaveAsync(new CorrectionRecord { Id = recordId, LeaseCaseId = fixture.LeaseCase.Id.Value,
            Purpose = "CaseScreeningAssessment", PayloadJson = JsonSerializer.Serialize(full, CaseScreeningService.JsonOptions) });
        fixture.LeaseCase.RecordScreeningResult(new(Guid.NewGuid(), fixture.LeaseCase.Id, fixture.Snapshot.Id.Value, ScreeningOutcome.Advisory,
            JsonSerializer.Serialize(new { schema = "c3-c4-reference-v1", recordId }), DateTime.UtcNow));
        var state = await sp.GetRequiredService<CaseScreeningService>().GetAsync(fixture.LeaseCase.Id.Value, null, null, default);
        Assert.Equal(storedParcel, state.ParcelId);
        var cleared = await sp.GetRequiredService<CaseScreeningService>().GetAsync(fixture.LeaseCase.Id.Value, null, Guid.Empty, default);
        Assert.Null(cleared.ParcelId);
        Assert.Equal(storedParcel, cleared.LatestAssessment!.ParcelId);
        Assert.False(cleared.IsCurrent);
        Assert.Equal(12000, state.LatestAssessment!.Assessment.GetProperty("findings").GetString()!.Length);
        Assert.Equal("TEST_BINDINGS", state.LatestAssessment.SourceBindingsJson);
        Assert.True(fixture.LeaseCase.LatestScreening!.Remarks!.Length < 2000);
    }

    [Theory]
    [InlineData("none", true)] [InlineData("legacy", false)] [InlineData("policy", false)] [InlineData("evidence", false)]
    [InlineData("input", false)] [InlineData("case", false)] [InlineData("snapshot", false)]
    public void CacheReuse_RequiresEveryFullContractBinding(string change, bool expected)
    {
        var input = StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures.C4DevelopmentCaseFixtureProvider.GetAgricultureProjectFixture()
            with { FullInputFingerprint = new string('a',64), EffectivePolicyVersion = "TEST_POLICY_1", EvidenceVersion = "TEST_EVIDENCE_1" };
        var result = JsonSerializer.Deserialize<ConsolidatedGovernanceScreeningResultDto>(JsonSerializer.Serialize(new {
            assessmentId = Guid.NewGuid(), input.CaseId, input.ProposalVersion, input.VerifiedFactSnapshotId,
            snapshotSha256 = StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands.OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(input),
            input.FullInputFingerprint, input.EffectivePolicyVersion, input.EvidenceVersion }), CaseScreeningService.JsonOptions)!;
        result = change switch {
            "legacy" => result with { FullInputFingerprint = null }, "policy" => result with { EffectivePolicyVersion = "OLD" },
            "evidence" => result with { EvidenceVersion = "OLD" }, "input" => result with { SnapshotSha256 = "OLD" },
            "case" => result with { CaseId = "OTHER" }, "snapshot" => result with { VerifiedFactSnapshotId = Guid.NewGuid() }, _ => result };
        Assert.Equal(expected, ConsolidatedCaseScreeningAdapter.HasFullBinding(result, input));
    }

    [Fact]
    public async Task ScreeningRefresh_ReadsThroughAnIndependentDependencyScope()
    {
        var services = new ServiceCollection(); services.AddWorkflowGovernanceApplication(); services.AddWorkflowGovernanceInfrastructure(true);
        services.AddScoped<IConsolidatedCaseScreeningAdapter, ScopeBoundAdapter>();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var fixture = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot();
        await scope.ServiceProvider.GetRequiredService<ILeaseCaseRepository>().AddAsync(fixture.LeaseCase);
        await scope.ServiceProvider.GetRequiredService<IGovernedDocumentRepository>().AddAsync(fixture.Document);
        await scope.ServiceProvider.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(fixture.Analysis);
        var service = scope.ServiceProvider.GetRequiredService<CaseScreeningService>();
        var before = await service.ReadInputRevisionAsync(fixture.LeaseCase.Id.Value, fixture.Snapshot.Id.Value, null, default);
        var refresh = await scope.ServiceProvider.GetRequiredService<IScreeningInputRefresh>()
            .ReadAsync(fixture.LeaseCase.Id.Value, fixture.Snapshot.Id.Value, null, default);
        var sameScope = await service.ReadInputRevisionAsync(fixture.LeaseCase.Id.Value, fixture.Snapshot.Id.Value, null, default);
        Assert.Equal(before.Prepared.InputFingerprint, sameScope.Prepared.InputFingerprint);
        Assert.NotEqual(before.Prepared.InputFingerprint, refresh.Prepared.InputFingerprint);
    }

    private sealed class ScopeBoundAdapter : IConsolidatedCaseScreeningAdapter
    {
        private readonly string fingerprint = Guid.NewGuid().ToString("D");
        public Task<PreparedCaseScreening> PrepareAsync(CaseScreeningSource source, CancellationToken ct) =>
            Task.FromResult(new PreparedCaseScreening(fingerprint, "TEST_POLICY", null, ["TEST ONLY"]));
        public Task<JsonElement> EvaluateAsync(PreparedCaseScreening prepared, CancellationToken ct) => throw new InvalidOperationException();
    }

    // Synthetic DTOs are test-only; no candidates or acceptance data are inserted into runtime.
    private static CaseScreeningSource Source()
    {
        var snapshot = JsonSerializer.Deserialize<VerifiedFactSnapshotDto>(JsonSerializer.Serialize(new {
            id = Guid.NewGuid(), entries = Array.Empty<VerifiedFactEntryDto>(), snapshotOutcome = "VerifiedFacts" }), CaseScreeningService.JsonOptions)!;
        return new(Guid.NewGuid(), "TEST-ONLY", snapshot, Array.Empty<GovernedDocumentDto>(), Guid.NewGuid());
    }
    private static JsonElement Result(CaseScreeningSource source) => JsonSerializer.SerializeToElement(new {
        assessmentId = Guid.NewGuid(), caseId = source.CaseId, verifiedFactSnapshotId = source.Snapshot!.Id,
        proposalVersion = "version:all-input-digest", overallStatus = "RequiresReview",
        officerRecommendationSummary = "Test-only officer review recommendation",
        consolidatedFindings = new[] { new { details = "Test recommendation", evidenceReference = "TEST-EVIDENCE" } } });
    private static PreparedCaseScreening Prepared() => new("inputs", "policy", "{\"proposalVersion\":\"version:all-input-digest\"}", Array.Empty<string>(), "test-bindings");

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Superseded", false)]
    [InlineData("Running", false)]
    public void Snapshot_BoundToUnfinishedOrSupersededRun_IsNotCurrent(string runState, bool expected)
    {
        var source = Source(); var docId = Guid.NewGuid(); var version = Guid.NewGuid(); var analysisId = Guid.NewGuid();
        var runId = Guid.NewGuid(); var resultId = Guid.NewGuid();
        var snapshot = source.Snapshot! with { GovernedDocumentId = docId, DocumentVersionId = version,
            DocumentAnalysisId = analysisId, AnalysisRunId = runId, AnalysisRunResultId = resultId,
            ChecksumAlgorithm = "SHA-256", ChecksumValue = "checksum" };
        var document = JsonSerializer.Deserialize<GovernedDocumentDto>(JsonSerializer.Serialize(new {
            id = docId, leaseCaseId = source.CaseId, activeVersionId = version,
            versions = new[] { new { id = version, checksumAlgorithm = "SHA-256", checksumValue = "checksum" } } }), CaseScreeningService.JsonOptions)!;
        var analysis = JsonSerializer.Deserialize<DocumentAnalysisDto>(JsonSerializer.Serialize(new {
            id = analysisId, governedDocumentId = docId, documentVersionId = version, checksumAlgorithm = "SHA-256", checksumValue = "checksum",
            activeRun = new { id = runId, state = runState, result = new { id = resultId } }, runs = Array.Empty<object>() }), CaseScreeningService.JsonOptions)!;
        Assert.Equal(expected, CaseScreeningService.IsSnapshotCurrentFor(source.CaseId, snapshot, document, analysis));
    }

    [Theory]
    [InlineData("case")]
    [InlineData("snapshot")]
    [InlineData("version")]
    [InlineData("emptyId")]
    [InlineData("missing")]
    public void ReturnedResult_MismatchedContract_IsRejected(string change)
    {
        var source = Source();
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Result(source).GetRawText())!;
        if (change == "case") payload["caseId"] = Guid.NewGuid();
        if (change == "snapshot") payload["verifiedFactSnapshotId"] = Guid.NewGuid();
        if (change == "version") payload["proposalVersion"] = "old-version";
        if (change == "emptyId") payload["assessmentId"] = Guid.Empty;
        if (change == "missing") payload.AsObject().Remove("verifiedFactSnapshotId");
        Assert.Throws<InvalidOperationException>(() => CaseScreeningService.ValidateResultBinding(JsonSerializer.SerializeToElement(payload), source, Prepared()));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("case", false)]
    [InlineData("snapshot", false)]
    [InlineData("parcel", false)]
    [InlineData("documents", false)]
    [InlineData("policy", false)]
    [InlineData("prerequisites", false)]
    public void StoredAssessment_ChangedInputsOrMissingPrerequisites_IsNotCurrent(string change, bool expected)
    {
        var source = Source(); var prepared = Prepared();
        var stored = new StoredCaseScreening("c3-c4-v1", source.CaseId, source.Snapshot!.Id, source.ParcelId, "inputs", "policy", Result(source), prepared.InputJson, prepared.BindingJson);
        if (change == "case") stored = stored with { CaseId = Guid.NewGuid() };
        if (change == "snapshot") stored = stored with { SnapshotId = Guid.NewGuid() };
        if (change == "parcel") source = source with { ParcelId = Guid.NewGuid() };
        if (change == "documents") prepared = prepared with { InputFingerprint = "changed-document-version" };
        if (change == "policy") prepared = prepared with { PolicyFingerprint = "changed-policy" };
        Assert.Equal(expected, CaseScreeningService.IsStoredCurrent(stored, source, prepared,
            change == "prerequisites" ? new[] { "Requirements not configured" } : Array.Empty<string>()));
    }

    [Fact]
    public void ScreeningRemarks_RoundTrip_PreservesFullAssessmentAndBindings()
    {
        var source = Source();
        var prepared = Prepared();
        var stored = new StoredCaseScreening("c3-c4-v1", source.CaseId, source.Snapshot!.Id, source.ParcelId, "inputs", "policy", Result(source), prepared.InputJson, prepared.BindingJson, Guid.NewGuid());
        var result = new ScreeningResult(Guid.NewGuid(), new(source.CaseId), source.Snapshot.Id, ScreeningOutcome.Advisory,
            JsonSerializer.Serialize(stored, CaseScreeningService.JsonOptions), DateTime.UtcNow);
        var roundTrip = PersistenceJsonSerializer.Deserialize<ScreeningResult>(PersistenceJsonSerializer.Serialize(result))!;
        var restored = JsonSerializer.Deserialize<StoredCaseScreening>(roundTrip.Remarks!, CaseScreeningService.JsonOptions)!;
        Assert.Equal(stored.InputFingerprint, restored.InputFingerprint);
        Assert.Equal(source.Snapshot.Id, restored.SnapshotId);
        Assert.Equal(stored.InputJson, restored.InputJson);
        Assert.Equal(stored.SourceBindingsJson, restored.SourceBindingsJson);
        Assert.Equal(stored.RequestedByOfficerId, restored.RequestedByOfficerId);
        Assert.Equal("Test-only officer review recommendation", restored.Assessment.GetProperty("officerRecommendationSummary").GetString());
        Assert.Equal("TEST-EVIDENCE", restored.Assessment.GetProperty("consolidatedFindings")[0].GetProperty("evidenceReference").GetString());
        Assert.Equal(ScreeningOutcome.Advisory, roundTrip.Outcome);
    }

    [Fact]
    public async Task MissingPrerequisites_DoesNotDispatchOrRecordPendingSuccess()
    {
        var services = new ServiceCollection(); services.AddWorkflowGovernanceApplication(); services.AddWorkflowGovernanceInfrastructure(true);
        var adapter = new NoDispatchAdapter(); services.AddSingleton<IConsolidatedCaseScreeningAdapter>(adapter);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope(); var sp = scope.ServiceProvider;
        var actor = Guid.NewGuid(); var caseId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var authority = new VerifiedAuthorityContext(actor, new[] { "LeaseInitiator" }, "LeaseCase", caseId.ToString("D"), now.AddMinutes(-1), now, now.AddMinutes(5));
        var created = await sp.GetRequiredService<ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>>().HandleAsync(new("TEST-BLOCKED", actor, authority, caseId));
        var state = await sp.GetRequiredService<CaseScreeningService>().RunAsync(caseId, new(Guid.Empty, created.Revision), actor, default);
        Assert.Equal("BlockedIncomplete", state.Status); Assert.False(state.IsCurrent); Assert.Equal(0, adapter.Calls);
        Assert.Null((await sp.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(caseId)))!.LatestScreening);
    }

    [Fact]
    public async Task C1Unavailable_DoesNotFabricateParcelOrClearConflictEvidence()
    {
        using var http = new HttpClient(new MissingC1Handler()); using var provider = new ServiceCollection().BuildServiceProvider();
        var adapter = new ConsolidatedCaseScreeningAdapter(http, Options.Create(new CaseScreeningOptions()), new UnavailableScreeningEvidenceProvider());
        var prepared = await adapter.PrepareAsync(Source() with { Snapshot = null }, default);
        Assert.Null(prepared.InputJson);
        Assert.Contains(prepared.MissingInputs, x => x.Contains("HTTP 404"));
        Assert.Contains(prepared.MissingInputs, x => x.Contains("Unknown cannot be sent"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.EvaluateAsync(prepared, default));
    }

    [Fact]
    public void FullFingerprint_ChangedComplaintContentsWithSameCount_ChangesBinding()
    {
        Assert.NotEqual(ConsolidatedCaseScreeningAdapter.Fingerprint(new { complaints = new[] { "evidence-v1" } }),
            ConsolidatedCaseScreeningAdapter.Fingerprint(new { complaints = new[] { "evidence-v2" } }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsolidatedMapping_PreservesC1AreaAndExactSnapshotEvidence_AndRejectsUnknownFlags(bool unknownFlag)
    {
        var source = Source();
        var facts = new Dictionary<string, string> { ["applicant_name"] = "TEST ONLY", ["proposed_land_use"] = "Agriculture",
            ["land_extent"] = "10", ["land_extent_unit"] = "acres", ["requested_lease_period"] = "30 years",
            ["investment_amount"] = "250000", ["investment_currency"] = "LKR", ["district"] = "Colombo", ["divisional_secretariat_division"] = "Colombo" };
        var entries = facts.Select(f => new { sourceExtractedFactId = Guid.NewGuid(), factCode = f.Key, effectiveValue = new { canonicalValue = f.Value } }).ToArray();
        var snapshot = JsonSerializer.Deserialize<VerifiedFactSnapshotDto>(JsonSerializer.Serialize(new {
            id = source.Snapshot!.Id, governedDocumentId = Guid.NewGuid(), documentVersionId = Guid.NewGuid(), publishedAt = DateTime.UtcNow, entries }), CaseScreeningService.JsonOptions)!;
        var pages = entries.Select(e => JsonSerializer.Deserialize<CandidateFactDto>(JsonSerializer.Serialize(new { id = e.sourceExtractedFactId, factCode = e.factCode, pageNumber = 7 }), CaseScreeningService.JsonOptions)!).ToList();
        source = source with { Snapshot = snapshot, FactEvidence = pages };
        var parcel = JsonSerializer.SerializeToElement(new { id = source.ParcelId, identifier = new { cadastralNumber = "TEST-C1" },
            area = new { value = 100m, unit = 2 }, location = new { province = "Western", district = "Colombo", divisionalSecretariat = "Colombo" },
            spatial = new { coordinateSystem = "EPSG:4326", centroidLatitude = 7d, centroidLongitude = 80d } });
        var governance = new GovernanceEvidence("TEST-ONLY-SOURCE", unknownFlag ? null : false, false, true, false, false,
            new[] { new ScreeningComplaintEvidenceDto("TEST-COMPLAINT", "Objection", "Test evidence", "Verified", DateTimeOffset.UtcNow) },
            Array.Empty<ConcurrentApplicationEvidenceDto>());
        var spatial = new SpatialReviewEvidence("TEST-ONLY-SURVEY", false, Array.Empty<C1AdjacentLeaseEvidenceDto>(), Array.Empty<C1SpatialConstraintItemDto>());
        var context = new ScreeningEvidenceContext("TEST-v1", JsonSerializer.Serialize(governance, CaseScreeningService.JsonOptions),
            JsonSerializer.Serialize(spatial, CaseScreeningService.JsonOptions), Array.Empty<string>(), source.CaseId, source.ParcelId);
        if (unknownFlag)
        {
            Assert.Throws<InvalidOperationException>(() => ConsolidatedCaseScreeningAdapter.BuildInput(source, parcel, context, "full-digest"));
            return;
        }
        var input = JsonSerializer.Deserialize<GovernanceScreeningCaseInputDto>(ConsolidatedCaseScreeningAdapter.BuildInput(source, parcel, context, "full-digest"), CaseScreeningService.JsonOptions)!;
        Assert.Equal(source.CaseId.ToString("D"), input.CaseId);
        Assert.Equal(snapshot.Id, input.VerifiedFactSnapshotId);
        Assert.Equal(100m, input.SpatialEvidence!.ExtentHectares); // Actual C1 parcel area, never the requested proposal area.
        Assert.Equal(4.0469m, input.ExtentHectares);
        Assert.True(input.ObjectionReceivedFlag);
        Assert.Single(input.ContemporaneousComplaints!);
        Assert.All(input.DocumentReferences!, reference => { Assert.Equal(7, reference.SourcePageNumber); Assert.Contains(snapshot.DocumentVersionId.ToString("D"), reference.StorageReference); });
        Assert.Equal(snapshot.DocumentVersionId.ToString("D"), input.ProposalVersion);
    }

    private sealed class NoDispatchAdapter : IConsolidatedCaseScreeningAdapter
    {
        public int Calls;
        public Task<PreparedCaseScreening> PrepareAsync(CaseScreeningSource source, CancellationToken ct) => Task.FromResult(new PreparedCaseScreening("inputs", "policy", null, new[] { "Real evidence missing" }));
        public Task<JsonElement> EvaluateAsync(PreparedCaseScreening prepared, CancellationToken ct) { Calls++; throw new InvalidOperationException("Must not dispatch"); }
    }
    private sealed class MissingC1Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
