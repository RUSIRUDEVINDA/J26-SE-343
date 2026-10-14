using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

internal static class KnowledgeGraphReferenceResolver
{
    public static Guid ResolveCategoryId(LandParcel parcel) =>
        LandIntelligenceSeedData.GetCategoryId(parcel.Category.Type);

    public static Guid? ResolveLandUseId(LandParcel parcel) =>
        parcel.CurrentUse is null ? null : LandIntelligenceSeedData.GetLandUseId(parcel.CurrentUse.Type);

    public static Guid ResolveAdministrativeAreaId(AdministrativeLocation location)
    {
        if (MatchesLocation(location, "Western", "Colombo", "Colombo DS"))
        {
            return KnowledgeGraphSeedData.SyntheticWesternColomboAreaId;
        }

        if (MatchesLocation(location, "Central", "Kandy", "Kandy DS"))
        {
            return KnowledgeGraphSeedData.SyntheticCentralKandyAreaId;
        }

        return CreateDeterministicGuid(
            "admin-area",
            location.Province,
            location.District,
            location.DivisionalSecretariat,
            location.GramaNiladhariDivision ?? string.Empty);
    }

    private static bool MatchesLocation(
        AdministrativeLocation location,
        string province,
        string district,
        string divisionalSecretariat) =>
        location.Province.Equals(province, StringComparison.OrdinalIgnoreCase)
        && location.District.Equals(district, StringComparison.OrdinalIgnoreCase)
        && location.DivisionalSecretariat.Equals(divisionalSecretariat, StringComparison.OrdinalIgnoreCase);

    private static Guid CreateDeterministicGuid(string scope, params string[] parts)
    {
        var payload = string.Join('\u001f', parts.Prepend(scope));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
