namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;
using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

internal sealed class WorkflowStageJsonConverter : JsonConverter<WorkflowStage>
{
    public override WorkflowStage Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var d = JsonDocument.ParseValue(ref reader);
        T Get<T>(string key) => d.RootElement.GetProperty(key).Deserialize<T>(options)!;
        return new(Get<WorkflowStageId>("id"), Get<WorkflowStageCode>("stageCode"), Get<InstitutionCode>("institutionCode"),
            Get<WorkflowStageType>("stageType"), Get<string>("requiredOfficerCapability"), Get<RoutingReasonCode>("routingReasonCode"),
            Get<string>("reasonDescription"), d.RootElement.TryGetProperty("targetDurationDays", out var days) ? days.GetInt32() : null,
            Get<bool>("isFinalDecision"), Get<List<WorkflowStageId>>("prerequisites"));
    }
    public override void Write(Utf8JsonWriter writer, WorkflowStage value, JsonSerializerOptions options)
    {
        var copy = new JsonSerializerOptions(options); copy.Converters.Remove(copy.Converters.OfType<WorkflowStageJsonConverter>().Single());
        JsonSerializer.Serialize(writer, value, copy);
    }
}
internal sealed class WorkflowPlanJsonConverter : JsonConverter<WorkflowPlan>
{
    public override WorkflowPlan Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var d = JsonDocument.ParseValue(ref reader);
        T Get<T>(string key) => d.RootElement.GetProperty(key).Deserialize<T>(options)!;
        var p = new WorkflowPlan(Get<WorkflowPlanId>("id"), Get<LeaseCaseId>("leaseCaseId"), Get<WorkflowPlanSource>("source"),
            Get<VerifiedFactSnapshotId>("verifiedFactSnapshotId"), Get<DocumentCompletenessAssessmentId>("documentCompletenessAssessmentId"),
            new WorkflowRuleSetReference(d.RootElement.GetProperty("ruleSetReference").GetProperty("identifier").GetString()!,
                d.RootElement.GetProperty("ruleSetReference").GetProperty("version").GetString()!),
            d.RootElement.TryGetProperty("recommendationReference", out var r) ? r.Deserialize<WorkflowRecommendationReference>(options) : null,
            Get<DateTime>("createdAt"), Get<List<WorkflowStage>>("stages"));
        foreach (var name in new[] { "Status", "Revision", "ApprovedAt", "SupersededAt", "SupersessionReason" })
        {
            var property = typeof(WorkflowPlan).GetProperty(name)!;
            var key = char.ToLowerInvariant(name[0]) + name[1..];
            if (d.RootElement.TryGetProperty(key, out var v)) AggregateRehydrator.SetPropertyBackingField(p, name, v.Deserialize(property.PropertyType, options));
        }
        AggregateRehydrator.SetField(p, "_domainEvents", new List<StateLandGovernance.BuildingBlocks.Events.IDomainEvent>());
        return p;
    }
    public override void Write(Utf8JsonWriter writer, WorkflowPlan value, JsonSerializerOptions options)
    {
        var copy = new JsonSerializerOptions(options); copy.Converters.Remove(copy.Converters.OfType<WorkflowPlanJsonConverter>().Single());
        JsonSerializer.Serialize(writer, value, copy);
    }
}
