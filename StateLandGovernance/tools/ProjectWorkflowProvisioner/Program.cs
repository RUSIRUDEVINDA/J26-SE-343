using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

if (args.Length == 1 && args[0] == "--provision-development-screening") { await PortalResearchProvisioner.RunAsync(); return; }

if (args.Length == 1 && args[0] == "--provision-development-review") { await PortalResearchProvisioner.RunAsync(reviewFixture: true); return; }

if (args.Length == 2 && args[0] == "--repair-intake-jurisdiction-projection")
{
    EnvFileLoader.LoadFromRepositoryRoot();
    var conn = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION") ?? throw new InvalidOperationException("Missing C3 connection.");
    var target = new Npgsql.NpgsqlConnectionStringBuilder(conn);
    if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "state_land_governance") throw new InvalidOperationException("Local development only.");
    await using var context = new WorkflowGovernanceDbContext(new DbContextOptionsBuilder<WorkflowGovernanceDbContext>().UseNpgsql(conn).Options);
    var result = await new StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories.PostgresCaseAssignmentStore(context)
        .RepairDeclaredJurisdictionAsync(Guid.Parse(args[1]));
    Console.WriteLine(JsonSerializer.Serialize(result)); return;
}

// Explicit sourced jurisdiction publication, never a browser grant or a startup seed.
if (args.Length == 2 && args[0] == "--publish-intake-jurisdiction")
{
    EnvFileLoader.LoadFromRepositoryRoot();
    var conn = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION") ?? throw new InvalidOperationException("Missing C3 connection.");
    var target = new Npgsql.NpgsqlConnectionStringBuilder(conn);
    if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "state_land_governance")
        throw new InvalidOperationException("Restricted to the intended local development database.");
    var policy = JsonSerializer.Deserialize<StateLandGovernance.WorkflowGovernance.Application.Interfaces.IntakeJurisdictionPolicy>(
        await File.ReadAllTextAsync(args[1]), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Policy required.");
    await using var context = new WorkflowGovernanceDbContext(new DbContextOptionsBuilder<WorkflowGovernanceDbContext>().UseNpgsql(conn).Options);
    await new StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories.PostgresCaseAssignmentStore(context).PublishPolicyAsync(policy);
    Console.WriteLine($"Published intake jurisdiction {policy.Identifier}/{policy.Version}; no case or review altered.");
    return;
}

// Explicit trusted local provisioning command, never invoked at host startup or from a browser.
if (args.Length != 3 || args[0] != "--publish-user-project" || !Guid.TryParse(args[2], out var publisher) || publisher == Guid.Empty)
    throw new ArgumentException("Usage: --publish-user-project <reviewed-template-json> <actual-development-provisioning-actor-id>");
EnvFileLoader.LoadFromRepositoryRoot();
var connection = Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION") ?? throw new InvalidOperationException("Missing C3 connection.");
var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection);
if (builder.Host is not ("localhost" or "127.0.0.1") || builder.Database != "state_land_governance")
    throw new InvalidOperationException("This command is restricted to the intended local development database.");
var options = new DbContextOptionsBuilder<WorkflowGovernanceDbContext>().UseNpgsql(connection).Options;
await using var db = new WorkflowGovernanceDbContext(options);
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
var root = json.RootElement;
if (root.GetProperty("authorizationKind").GetString() != "UserSpecifiedProjectRequirements")
    throw new InvalidOperationException("Only the explicit user-specified project configuration is accepted.");
var source = root.GetProperty("sourceReference").GetString()!;
var id = new ProposalTemplateId(root.GetProperty("templateId").GetString()!);
var version = root.GetProperty("version").GetString()!;
var requirements = root.GetProperty("requirements").EnumerateArray().Select((r, index) => new ProposalContentRequirement(
    new(r.GetProperty("id").GetString()!), ProposalRequirementKind.Section, r.GetProperty("name").GetString()!,
    r.GetProperty("mandatory").GetBoolean(), id, version, source + "; template page " + r.GetProperty("page").GetInt32(), index + 1,
    r.TryGetProperty("applicability", out var condition) ? condition.GetString() : null)).ToArray();
var template = new ProposalTemplate(id, version, root.GetProperty("name").GetString()!,
    source + $"; provisioning actor {publisher:D} (local development publisher, not an official/statutory approver)", ProposalTemplateStatus.Active, requirements);
var writer = new CompletenessConfigurationWriter(db);
var existing = await new PostgresProposalTemplateProvider(db).GetTemplateAsync(id, version);
if (existing == null) await writer.PublishTemplateAsync(template, publisher);
else if (existing.DefinitionDigest != template.DefinitionDigest) throw new InvalidOperationException("Existing version differs; publish a new version, never overwrite.");
var documentId = root.GetProperty("documentRequirementSetId").GetString()!;
var docs = new[] { new DocumentRequirementSnapshot(new(new Guid("fa430000-0000-4000-8000-000000000001")), new("Proposal"),
    RequirementCriticality.Mandatory, RequirementApplicability.Required, 1, "USER_PROJECT_PROPOSAL_INTAKE",
    "Proposal at intake, supplied user workflow/template. Other role-specific documents remain ongoing fulfillment requirements, not invented intake prerequisites.") };
if (await new PostgresDocumentRequirementProvider(db).GetRequirementSetAsync(documentId, version) == null)
    await writer.PublishDocumentsAsync(documentId, version, template.AuthoritativeSourceReference, publisher, docs);
Console.WriteLine(JsonSerializer.Serialize(new { authorizationKind = "UserSpecifiedProjectRequirements", templateId = id.Value,
    version, requirementCount = requirements.Length, definitionDigest = template.DefinitionDigest,
    provisioningActorId = publisher, documentRequirementSetId = documentId,
    officialApprovalClaimed = false, runtimeSeedsInstalled = false }));
