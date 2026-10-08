using System.Security.Cryptography;
using System.Text;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public static class GisDerivedIntelligenceIdentity
{
    public static Guid CreateSnapshotId(Guid parcelId) =>
        CreateDeterministicId(parcelId, "snapshot", parcelId);

    public static Guid CreateSoilGroupRecordId(Guid parcelId) =>
        CreateDeterministicId(parcelId, "soil-group", parcelId);

    public static Guid CreateRoadInfrastructureId(Guid parcelId, Guid roadReferenceId) =>
        CreateDeterministicId(parcelId, "road", roadReferenceId);

    public static Guid CreateWaterInfrastructureId(Guid parcelId, Guid waterReferenceId) =>
        CreateDeterministicId(parcelId, "water", waterReferenceId);

    public static Guid CreateConservationRestrictionId(Guid parcelId, Guid conservationAreaId) =>
        CreateDeterministicId(parcelId, "conservation", conservationAreaId);

    internal static Guid CreateDeterministicId(Guid parcelId, string category, Guid referenceId)
    {
        var input = $"{parcelId:N}:{category}:{referenceId:N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
