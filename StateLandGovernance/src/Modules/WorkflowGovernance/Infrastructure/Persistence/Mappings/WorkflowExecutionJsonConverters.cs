namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

/// <summary>Trusted committed-state rehydration only; never used for browser request bodies.</summary>
internal sealed class WorkflowExecutionJsonConverterFactory : JsonConverterFactory
{
    private static readonly HashSet<Type> Supported = new()
    {
        typeof(WorkflowExecution), typeof(WorkflowExecutionDefinition), typeof(WorkflowStageDefinition),
        typeof(WorkflowStageExecution), typeof(WorkflowStageDecision), typeof(WorkflowPrerequisiteGatePolicy),
        typeof(ConsensusAssessment)
    };
    public override bool CanConvert(Type type) => Supported.Contains(type);
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ExecutionStateConverter<>).MakeGenericType(type))!;

    private sealed class ExecutionStateConverter<T> : JsonConverter<T> where T : class
    {
        private static readonly Dictionary<string, string> CollectionFields = new()
        {
            ["Stages"] = "_stages", ["Prerequisites"] = "_prerequisites", ["Conditions"] = "_conditions",
            ["ApprovalRequiredStages"] = "_approvalRequiredStages", ["DecisionHistory"] = "_decisionHistory",
            ["PlanHistory"] = "_planHistory", ["ConsensusHistory"] = "_consensusHistory",
            ["ReasonCodes"] = "_reasonCodes", ["ParticipantResults"] = "_participantResults"
        };

        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var value = AggregateRehydrator.Create<T>();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == "DomainEvents") continue;
                var fieldName = CollectionFields.GetValueOrDefault(property.Name, $"<{property.Name}>k__BackingField");
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new JsonException($"Unsupported execution state property {property.Name}.");
                var key = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                if (!document.RootElement.TryGetProperty(key, out var element))
                {
                    // Additive history fields default to empty for older committed state.
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        field.SetValue(value, Activator.CreateInstance(field.FieldType));
                    else if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null)
                        throw new JsonException($"Missing execution state property {key}.");
                    continue;
                }
                object? restored;
                if (property.PropertyType == typeof(WorkflowRuleSetReference))
                    restored = new WorkflowRuleSetReference(element.GetProperty("identifier").GetString()!, element.GetProperty("version").GetString()!);
                else restored = element.Deserialize(field.FieldType, options);
                field.SetValue(value, restored);
            }
            if (type == typeof(WorkflowExecution))
            {
                if (((WorkflowExecution)(object)value).Definition == null)
                    throw new JsonException("Execution definition binding is required.");
                AggregateRehydrator.SetField(value, "_domainEvents", new List<IDomainEvent>());
            }
            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == "DomainEvents") continue;
                var item = property.GetValue(value);
                if (item == null && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull) continue;
                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name);
                JsonSerializer.Serialize(writer, item, property.PropertyType, options);
            }
            writer.WriteEndObject();
        }
    }
}
