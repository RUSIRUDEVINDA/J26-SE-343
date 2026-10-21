namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

public sealed class CaseScreeningOptions
{
    public string C1BaseUrl { get; set; } = "http://localhost:5264";
    public bool EnableDevelopmentSpatialMocks { get; set; }
    public string? DevelopmentSpatialFixturePath { get; set; }
}

public sealed class UnavailableScreeningEvidenceProvider : IScreeningEvidenceProvider
{
    public Task<ScreeningEvidenceContext> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken ct)
        => Task.FromResult(new ScreeningEvidenceContext(null, null, null, new[] {
            "Source-backed complaint/objection, occupation, dispute, inheritance and prior-activity evidence states are unavailable. Unknown cannot be sent to C4 as false.",
            "Authoritative concurrent-application and adjacent-lease coverage is unavailable. An empty query or omitted array is not proof of no conflict.",
            "C1's parcel/constraints/relationships read contracts do not establish authoritative parcel-overlap clearance." }));
}

/// <summary>ACL to C4's existing consolidated handler. No screening rules are duplicated here.</summary>
public sealed class ConsolidatedCaseScreeningAdapter(HttpClient http, IOptions<CaseScreeningOptions> options,
    IScreeningEvidenceProvider evidence, IRegulatoryRuleProvider? policy = null,
    OrchestrateGovernanceScreeningCommandHandler? handler = null,
    IConsolidatedGovernanceScreeningStore? consolidatedStore = null,
    IDevelopmentSpatialInputProvider? developmentSpatial = null) : IConsolidatedCaseScreeningAdapter
{
    private static readonly JsonSerializerOptions Json = CaseScreeningService.JsonOptions;
    public static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)))).ToLowerInvariant();

    public async Task<PreparedCaseScreening> PrepareAsync(CaseScreeningSource source, CancellationToken ct)
    {
        _ = policy; // Retain the legacy constructor contract; consolidated policy now comes from C4's approved catalogue port.
        var missing = new List<string>();
        var review = await evidence.ReadAsync(source.CaseId, source.ParcelId, ct);
        var mock = developmentSpatial == null ? null : await developmentSpatial.ReadAsync(source.CaseId, source.ParcelId, ct);
        if (mock != null)
        {
            source = source with { ParcelId = mock.ParcelId };
            // Only spatial input is mocked. Unknown governance/complaint coverage remains unavailable.
            review = review with { SpatialReviewJson = mock.SpatialReviewJson,
                Version = (review.Version ?? "Governance-unavailable") + ":" + mock.Version };
        }
        missing.AddRange(review.MissingInputs);
        if (mock == null && (review.BoundCaseId != source.CaseId || review.BoundParcelId != source.ParcelId || review.BoundParcelId is null))
            missing.Add("An authoritative case-to-parcel association is required. A parcel selected in the browser is not proof of that association.");
        if (review.Version is null || review.GovernanceJson is null || review.SpatialReviewJson is null)
            missing.Add("A versioned authoritative governance/spatial review evidence source has not been connected.");
        JsonElement? parcel = null, constraints = null, relationships = null;
        if (mock != null) { parcel = mock.Parcel; constraints = JsonSerializer.SerializeToElement(new { mock.Version, mock.Label }); relationships = constraints; }
        else if (source.ParcelId is null) missing.Add("Select the actual C1 parcel ID linked to this application; proposal location text is not a parcel identifier.");
        else
        {
            var baseUri = new Uri(options.Value.C1BaseUrl.TrimEnd('/') + "/");
            foreach (var suffix in new[] { "", "/constraints", "/relationships" })
            {
                try
                {
                    using var response = await http.GetAsync(new Uri(baseUri, $"api/v1/land/parcels/{source.ParcelId:D}{suffix}"), ct);
                    if (!response.IsSuccessStatusCode) { missing.Add($"C1 parcel{suffix} returned HTTP {(int)response.StatusCode}; spatial evidence is unavailable."); continue; }
                    var content = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
                    if (suffix == "") parcel = content; else if (suffix == "/constraints") constraints = content; else relationships = content;
                }
                catch (HttpRequestException) { missing.Add($"C1 parcel{suffix} service is unavailable."); }
                catch (JsonException) { missing.Add($"C1 parcel{suffix} returned an invalid response contract."); }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { missing.Add($"C1 parcel{suffix} lookup timed out."); }
            }
        }
        if (parcel is { } p && (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty("id", out var parcelKey)
            || parcelKey.ValueKind != JsonValueKind.String || !parcelKey.TryGetGuid(out var returnedParcel) || returnedParcel != source.ParcelId))
            missing.Add("C1 returned a different parcel identifier.");
        if (handler is null) missing.Add("C4 consolidated orchestration is not registered in this host.");
        var approvedPolicy = handler == null ? null : await handler.ReadEffectivePolicyAsync(ct);
        var policyBinding = new { sourceReference = approvedPolicy?.SourceReference, version = approvedPolicy?.Version, rules = approvedPolicy?.Rules,
            engineModule = typeof(OrchestrateGovernanceScreeningCommandHandler).Assembly.ManifestModule.ModuleVersionId };
        var policyHash = Fingerprint(policyBinding);
        if (approvedPolicy == null)
            missing.Add("C4 approved active catalogue policy manifest/version is not configured; the legacy baseline cannot establish approval.");
        var bindings = new { source, parcel, constraints, relationships, review, policy = policyBinding,
            isDevelopmentMock = mock != null, spatialProvenance = mock };
        var bindingJson = JsonSerializer.Serialize(bindings, Json);
        var hash = Fingerprint(bindings);
        string? inputJson = null;
        if (source.Snapshot is null) missing.Add("Verified facts are unavailable for the C4 request contract.");
        else
        {
            var facts = source.Snapshot.Entries.ToDictionary(x => x.FactCode, x => x.EffectiveValue.CanonicalValue, StringComparer.OrdinalIgnoreCase);
            if (source.Snapshot.Entries.Any(e => (e.HumanEvidence?.PageNumber ?? source.FactEvidence?.SingleOrDefault(f => f.Id == e.SourceExtractedFactId)?.PageNumber) is null or <= 0))
                missing.Add("Every submitted verified fact requires its existing exact-version source page; page numbers will not be invented.");
            foreach (var code in new[] { "applicant_name", "proposed_land_use", "land_extent", "land_extent_unit", "requested_lease_period", "investment_amount", "investment_currency", "district", "divisional_secretariat_division" })
                if (!facts.TryGetValue(code, out var value) || string.IsNullOrWhiteSpace(value)) missing.Add($"C4 screening input requires the verified fact '{code}'. This is not a proposal-section requirement.");
            if (missing.Count == 0 && parcel.HasValue)
            {
                try { inputJson = BuildInput(source, parcel.Value, review, hash);
                    var input = JsonSerializer.Deserialize<GovernanceScreeningCaseInputDto>(inputJson, Json)!;
                    inputJson = JsonSerializer.Serialize(input with { FullInputFingerprint = hash, EffectivePolicyVersion = approvedPolicy!.Version,
                        EvidenceVersion = review.Version, IsDevelopmentSpatialMock = mock != null }, Json); }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException or JsonException)
                { missing.Add("C4 input contract is incomplete: " + ex.Message); }
            }
        }
        return new(hash, policyHash, inputJson, missing.Distinct().ToList(), bindingJson, mock != null, mock?.Label);
    }

    // Called only after coverage checks. Missing fields never acquire default false/empty evidence.
    public static string BuildInput(CaseScreeningSource source, JsonElement parcel, ScreeningEvidenceContext review, string fingerprint)
    {
        var snapshot = source.Snapshot ?? throw new InvalidOperationException("A verified snapshot is required.");
        var facts = snapshot.Entries.ToDictionary(x => x.FactCode, x => x.EffectiveValue.CanonicalValue, StringComparer.OrdinalIgnoreCase);
        var governance = JsonSerializer.Deserialize<GovernanceEvidence>(review.GovernanceJson!, Json)
            ?? throw new InvalidOperationException("Governance evidence is unavailable.");
        var spatialReview = JsonSerializer.Deserialize<SpatialReviewEvidence>(review.SpatialReviewJson!, Json)
            ?? throw new InvalidOperationException("Spatial review is unavailable.");
        if (string.IsNullOrWhiteSpace(governance.SourceReference) || governance.ContemporaneousComplaints is null
            || governance.ConcurrentApplications is null || spatialReview.AdjacentLeases is null || spatialReview.SpatialConstraints is null
            || spatialReview.HasOverlappingParcels is null || string.IsNullOrWhiteSpace(spatialReview.SourceReference)
            || governance.VerifiedLegalDisputeFlag is null || governance.UnauthorizedOccupationFlag is null
            || governance.ObjectionReceivedFlag is null || governance.FamilyOrInheritanceClaimFlag is null || governance.DocumentedPriorActivityFlag is null)
            throw new InvalidOperationException("Authoritative review must explicitly cover every boolean and collection; unknown is not absence.");
        var extent = LandExtentNormalizer.TryNormalizeToHectares(facts["land_extent"], facts["land_extent_unit"]);
        var years = LeasePeriodNormalizer.TryParseYears(facts["requested_lease_period"]);
        var investment = InvestmentNormalizer.NormalizeInvestment(facts["investment_amount"], facts["investment_currency"]);
        if (extent is null or <= 0 || years is null or <= 0 || investment.Amount is null || !investment.IsLkr)
            throw new InvalidOperationException("Verified extent, lease duration or investment/currency cannot be normalized to C4's contract without inventing values.");
        var loc = parcel.GetProperty("location");
        var spatial = parcel.GetProperty("spatial");
        var area = parcel.GetProperty("area");
        var areaUnit = area.GetProperty("unit");
        // Numeric values are the published C1 AreaUnit contract (1 m², 2 ha, 3 acres).
        var unit = areaUnit.ValueKind == JsonValueKind.Number ? areaUnit.GetInt32() switch
            { 1 => "m2", 2 => "ha", 3 => "acres", _ => null } : areaUnit.GetString();
        var parcelExtent = LandExtentNormalizer.TryNormalizeToHectares(
            area.GetProperty("value").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture), unit);
        if (parcelExtent is null or <= 0) throw new InvalidOperationException("C1 parcel extent/unit is unavailable or unsupported.");
        var parcelEvidence = new C1ParcelEvidenceDto(source.ParcelId!.Value.ToString("D"),
            parcel.GetProperty("identifier").GetProperty("cadastralNumber").GetString()!, parcelExtent.Value,
            loc.GetProperty("province").GetString()!, loc.GetProperty("district").GetString()!, loc.GetProperty("divisionalSecretariat").GetString()!,
            loc.TryGetProperty("gramaNiladhariDivision", out var gn) ? gn.GetString() : null,
            spatial.GetProperty("coordinateSystem").GetString()!, spatial.GetProperty("centroidLatitude").GetDouble(),
            spatial.GetProperty("centroidLongitude").GetDouble(), spatial.TryGetProperty("boundaryPolygon", out var boundary) && boundary.ValueKind != JsonValueKind.Null ? boundary.GetRawText() : null,
            SpatialConstraints: spatialReview.SpatialConstraints, AdjacentLeases: spatialReview.AdjacentLeases,
            HasOverlappingParcels: spatialReview.HasOverlappingParcels.Value, OverlapDetails: spatialReview.SourceReference);
        var references = snapshot.Entries.Select(x => new ScreeningDocumentReferenceDto(x.SourceKind,
            x.FactCode, $"/api/workflow-governance/cases/{source.CaseId:D}/documents/{snapshot.GovernedDocumentId:D}/versions/{snapshot.DocumentVersionId:D}/download",
            x.HumanEvidence?.PageNumber ?? source.FactEvidence?.SingleOrDefault(f => f.Id == x.SourceExtractedFactId)?.PageNumber
                ?? throw new InvalidOperationException("Verified fact page provenance is required."), x.FactCode)).ToList();
        var input = new GovernanceScreeningCaseInputDto(source.CaseId.ToString("D"), source.ApplicationReference,
            snapshot.DocumentVersionId.ToString("D"), snapshot.Id, snapshot.PublishedAt, source.ParcelId.Value.ToString("D"),
            parcelEvidence.CadastralNumber, facts["applicant_name"], LandUseNormalizer.TryNormalizeLandUse(facts["proposed_land_use"]) ?? facts["proposed_land_use"],
            extent.Value, years.Value, investment.Amount.Value, investment.Currency!, governance.ZoningArea,
            facts["district"], facts["divisional_secretariat_division"], parcelEvidence.Province, references,
            governance.VerifiedLegalDisputeFlag.Value, governance.UnauthorizedOccupationFlag.Value,
            governance.ObjectionReceivedFlag.Value, governance.FamilyOrInheritanceClaimFlag.Value, governance.DocumentedPriorActivityFlag.Value,
            governance.ContemporaneousComplaints, governance.FinancialAssessmentReference, parcelEvidence, governance.ConcurrentApplications);
        return JsonSerializer.Serialize(input, Json);
    }

    public async Task EnsureCanEvaluateAsync(PreparedCaseScreening prepared, CancellationToken ct)
    {
        if (prepared.MissingInputs.Count != 0 || prepared.InputJson is null) throw new InvalidOperationException("Screening inputs are incomplete.");
        var input = JsonSerializer.Deserialize<GovernanceScreeningCaseInputDto>(prepared.InputJson, Json)!;
        if (handler is null) throw new InvalidOperationException("C4 consolidated orchestration is unavailable.");
        if (consolidatedStore is null) throw new InvalidOperationException("C4 cache provenance cannot be checked.");
        var cached = await consolidatedStore.FindByIdempotencyKeyAsync(input.CaseId, input.ProposalVersion,
            OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(input), ct);
        if (cached is not null && !HasFullBinding(cached, input))
            throw new InvalidOperationException("C4 has a cached result whose full input/policy fingerprint cannot be verified. Reassessment requires a C4 full-input idempotency contract; proposal version identity is preserved.");
    }
    public async Task<JsonElement> EvaluateAsync(PreparedCaseScreening prepared, CancellationToken ct)
    {
        await EnsureCanEvaluateAsync(prepared, ct);
        var input = JsonSerializer.Deserialize<GovernanceScreeningCaseInputDto>(prepared.InputJson!, Json)!;
        var requestedAssessmentId = Guid.NewGuid();
        var result = await handler!.HandleAsync(new(input, requestedAssessmentId), ct);
        if (!HasFullBinding(result, input))
            throw new InvalidOperationException("C4 result/cache does not carry the exact complete current input, policy and evidence contract.");
        if (result.SnapshotSha256 != OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(input))
            throw new InvalidOperationException("C4 returned a different input hash.");
        if (result.FullInputFingerprint != input.FullInputFingerprint || result.EffectivePolicyVersion != input.EffectivePolicyVersion || result.EvidenceVersion != input.EvidenceVersion)
            throw new InvalidOperationException("C4 did not echo the exact full-input/policy/evidence bindings.");
        return JsonSerializer.SerializeToElement(result, Json);
    }

    public static bool HasFullBinding(ConsolidatedGovernanceScreeningResultDto result, GovernanceScreeningCaseInputDto input) =>
        result.AssessmentId != Guid.Empty && input.FullInputFingerprint is { Length: 64 } &&
        !string.IsNullOrWhiteSpace(input.EffectivePolicyVersion) && !string.IsNullOrWhiteSpace(input.EvidenceVersion) &&
        result.CaseId == input.CaseId && result.VerifiedFactSnapshotId == input.VerifiedFactSnapshotId &&
        result.ProposalVersion == input.ProposalVersion && result.SnapshotSha256 == OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(input) &&
        result.FullInputFingerprint == input.FullInputFingerprint && result.EffectivePolicyVersion == input.EffectivePolicyVersion &&
        result.EvidenceVersion == input.EvidenceVersion;
}

public sealed record GovernanceEvidence(string SourceReference, bool? VerifiedLegalDisputeFlag,
    bool? UnauthorizedOccupationFlag, bool? ObjectionReceivedFlag, bool? FamilyOrInheritanceClaimFlag,
    bool? DocumentedPriorActivityFlag, IReadOnlyList<ScreeningComplaintEvidenceDto>? ContemporaneousComplaints,
    IReadOnlyList<ConcurrentApplicationEvidenceDto>? ConcurrentApplications,
    string? ZoningArea = null, string? FinancialAssessmentReference = null);
public sealed record SpatialReviewEvidence(string SourceReference, bool? HasOverlappingParcels,
    IReadOnlyList<C1AdjacentLeaseEvidenceDto>? AdjacentLeases, IReadOnlyList<C1SpatialConstraintItemDto>? SpatialConstraints);
