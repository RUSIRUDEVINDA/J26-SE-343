namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

// Frozen domain constructors accept IEnumerable while their properties expose read-only
// collections. System.Text.Json cannot bind those types automatically. Rehydrate through
// the public constructors so their invariants and original review/history metadata survive.
internal sealed class ProposalTemplateSnapshotJsonConverter : JsonConverter<ProposalTemplateSnapshot>
{
    public override ProposalTemplateSnapshot Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        T Get<T>(string name) => root.GetProperty(name).Deserialize<T>(options)!;
        return new(Get<ProposalTemplateId>("templateId"), Get<string>("templateVersion"), Get<string>("name"),
            Get<string>("authoritativeSourceReference"), Get<ProposalTemplateStatus>("status"),
            Get<List<ProposalContentRequirement>>("requirements"),
            root.TryGetProperty("effectivePeriod", out var period) ? period.Deserialize<ProposalTemplateEffectivePeriod>(options) : null);
    }
    public override void Write(Utf8JsonWriter writer, ProposalTemplateSnapshot value, JsonSerializerOptions options)
    {
        var copy = new JsonSerializerOptions(options);
        copy.Converters.Remove(copy.Converters.OfType<ProposalTemplateSnapshotJsonConverter>().Single());
        JsonSerializer.Serialize(writer, value, copy);
    }
}

internal sealed class ProposalContentResultJsonConverter : JsonConverter<ProposalContentCompletenessResult>
{
    public override ProposalContentCompletenessResult Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        T Get<T>(string name) => root.TryGetProperty(name, out var value) ? value.Deserialize<T>(options)! : default!;
        return new(Get<ProposalContentAssessmentResultId>("id"), Get<LeaseCaseId>("leaseCaseId"),
            Get<ProposalSourceBinding>("sourceBinding"), Get<ProposalTemplateSnapshot>("templateSnapshot"),
            Get<ProposalContentCompletenessOutcome>("outcome"),
            Get<List<ProposalContentRequirement>>("satisfiedMandatory"), Get<List<ProposalContentRequirement>>("missingMandatory"),
            Get<List<ProposalContentRequirement>>("reviewRequiredMandatory"), Get<List<ProposalContentRequirement>>("missingOptional"),
            Get<List<ProposalContentRequirement>>("satisfiedOptional"), Get<List<string>>("explanations"),
            Get<List<ProposalContentRequirement>>("unresolvedOptional"), Get<bool>("isConfirmed"),
            Get<Guid?>("confirmedByActorId"), Get<DateTime?>("confirmedAtUtc"), Get<string?>("confirmationNotes"),
            Get<ProposalContentAssessmentResultId?>("confirmedResultOfId"), Get<ProposalContentAssessmentResultId?>("supersedesResultId"),
            Get<string?>("correctionReason"), Get<string?>("correctionEvidenceReference"), Get<int>("sequenceNumber"));
    }
    public override void Write(Utf8JsonWriter writer, ProposalContentCompletenessResult value, JsonSerializerOptions options)
    {
        var copy = new JsonSerializerOptions(options);
        copy.Converters.Remove(copy.Converters.OfType<ProposalContentResultJsonConverter>().Single());
        JsonSerializer.Serialize(writer, value, copy);
    }
}
