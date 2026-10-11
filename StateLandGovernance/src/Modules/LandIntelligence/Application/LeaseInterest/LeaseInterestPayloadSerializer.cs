using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

public static class LeaseInterestPayloadSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(UpsertLeaseInterestDraftDto draft) =>
        JsonSerializer.Serialize(draft, Options);

    public static UpsertLeaseInterestDraftDto Deserialize(string json)
    {
        var draft = JsonSerializer.Deserialize<UpsertLeaseInterestDraftDto>(json, Options);
        if (draft is null)
        {
            throw new InvalidOperationException("Lease interest payload could not be deserialized.");
        }

        return draft;
    }
}
