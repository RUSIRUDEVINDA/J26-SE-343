using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;

public static class KiwulaCatalogue
{
    public const string ProposalId = "kiulakale-demonstration-adaptation-v1";
    public const string Version = "kiwula-proposal-workspace/1";
    public const string Prefix = "C4-KIWULA-EXAMPLE:";
    public const string WorkflowScenario = "kiwula-kale-example-v1";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static IReadOnlyList<GovernanceDecisionSnapshotDto> Decisions => [
        new("example-land-review", ProposalId, "Example local land review (not an official institution decision)", "Local", "Condition", "Coconut cultivation", new(2027,1,5,0,0,0,DateTimeKind.Utc), new(2027,12,31,0,0,0,DateTimeKind.Utc), "example://kiwula/v1/land-review"),
        new("example-sector-review", ProposalId, "Example agricultural review (confirmation required)", "Local", "Condition", "Coconut cultivation", new(2027,1,8,0,0,0,DateTimeKind.Utc), new(2027,12,31,0,0,0,DateTimeKind.Utc), "example://kiwula/v1/sector-review")
    ];
    public static IReadOnlyList<EarlyGovernanceIndicatorDto> Indicators => Enum.GetNames<EarlyGovernanceIndicatorType>()
        .Select(x => new EarlyGovernanceIndicatorDto(x, "Missing")).ToArray();
    public static object Preview => new {
        proposalId = ProposalId, displayName = "Project_Proposal_Kiwula Kale", version = Version,
        originalName = "Kiwula Kale English adaptation – independent PP1 regulatory assessment",
        source = "English PP1 adaptation; original pp4–6; not applicant-confirmed or official evidence",
        location = new { district = "Puttalam", divisionalSecretariat = "Mahakumbukkadawala", gn = "614/F Kiwula No. 01", localAuthority = "Anamaduwa", province = "Unknown" },
        extent = "17 acres / 6.8797 hectares stated in adaptation; not a verified survey",
        proposedUse = "Coconut cultivation; future dairy feasibility only", tenure = "15-year request is an adaptation assumption; lawful tenure unknown",
        unknowns = new[] { "Province and province coverage", "Parcel/survey identifier and boundaries", "Land classification, ownership and access rights", "Official decisions, consultation and environmental evidence", "Approved routing policy and external department selections for this proposal" },
        routingSource = "WorkflowGovernance/Application/Commands/ProjectWorkflowGraph.cs (reference only; no integration)",
        workflow = new[] {
            new { code="EXTERNAL", institution="Agricultural / environmental / land technical review", status="Proposed review — confirmation required", dependsOn="None; relevant selected departments could review in parallel", reason="Coconut use and future dairy concept; no approved department selection supplied" },
            new { code="DS", institution="Divisional Secretariat review", status="Required by configured project topology, conditional on an approved bound policy", dependsOn="Selected external reviews", reason="Source location names Mahakumbukkadawala; jurisdiction still requires authoritative confirmation" },
            new { code="REGIONAL", institution="Provincial or interprovincial land review", status="Unknown because verified province coverage is missing", dependsOn="DS review", reason="One province selects provincial; multiple verified provinces select interprovincial; no boundary-distance inference" },
            new { code="COMMISSIONER", institution="Commissioner-level review", status="Required by configured project topology, conditional on an approved bound policy", dependsOn="Regional outcome, including non-recommendation", reason="Independent explicit final decision in the reference topology; no official decision here" },
            new { code="CABINET", institution="Cabinet review", status="Not triggered by stated 17 acres; applicability unconfirmed until extent is verified", dependsOn="Would precede external reviews if applicable", reason="Existing project threshold strictly greater than 50 acres; no rule change or legal determination" }
        }, decisions = Decisions, indicators = Indicators, workflowScenario = WorkflowScenario
    };
}

public sealed record KiwulaRunRequest(Guid SubmissionKey, string Engine);
public sealed record KiwulaComputed(JsonElement Result);
public sealed record KiwulaReport(Guid Id, Guid CreatorActorId, Guid SubmissionKey, string Engine,
    string ProposalId, string InputVersion, string InputHash, string EngineVersion, DateTimeOffset CreatedAtUtc,
    Guid AssessmentId, Guid? AuditRecordId, string SourceReference, string EngineArtifactHash, string InputJson, JsonElement Input, JsonElement Result);
public interface IKiwulaAssessmentStore
{
    Task<bool> ReadyAsync(CancellationToken ct);
    Task<IReadOnlyList<KiwulaReport>> ListAsync(Guid actor, CancellationToken ct);
    Task<KiwulaReport> GetAsync(Guid actor, Guid id, CancellationToken ct);
    Task<KiwulaReport> RunAsync(Guid actor, KiwulaRunRequest request, string inputJson,
        Func<Guid, Task<KiwulaComputed>> compute, CancellationToken ct);
}
public sealed class KiwulaSubmissionConflictException : Exception;
public sealed class KiwulaUnavailableException : Exception;
public sealed class KiwulaAssessmentService(IKiwulaAssessmentStore store,
    DetectConflictsCommandHandler conflicts, ScreenEarlyGovernanceCommandHandler screening)
{
    public Task<KiwulaReport> RunAsync(Guid actor, KiwulaRunRequest request, CancellationToken ct)
    {
        if (actor == Guid.Empty) throw new UnauthorizedAccessException();
        if (request.SubmissionKey == Guid.Empty || request.Engine is not ("conflict" or "early")) throw new ArgumentException("Select a supported engine and submission key.");
        var input = request.Engine == "conflict" ? JsonSerializer.Serialize(KiwulaCatalogue.Decisions, KiwulaCatalogue.Json)
            : JsonSerializer.Serialize(KiwulaCatalogue.Indicators, KiwulaCatalogue.Json);
        return store.RunAsync(actor, request, input, async id => {
            object result = request.Engine == "conflict"
                ? await conflicts.HandleAsync(new(KiwulaCatalogue.Prefix + id, KiwulaCatalogue.Decisions), ct)
                : await screening.HandleAsync(new(id, id, KiwulaCatalogue.Prefix + id, KiwulaCatalogue.Version, KiwulaCatalogue.Indicators), ct);
            return new(JsonSerializer.SerializeToElement(result, KiwulaCatalogue.Json));
        }, ct);
    }
}
