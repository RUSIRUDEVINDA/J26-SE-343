using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandSearchRequestNormalizer
{
    public static LandSearchRequest Normalize(LandSearchRequest request) =>
        request with
        {
            Province = TrimOrNull(request.Province),
            District = TrimOrNull(request.District),
            DivisionalSecretariat = TrimOrNull(request.DivisionalSecretariat),
            CadastralNumber = TrimOrNull(request.CadastralNumber),
            SourceReportedLandUse = TrimOrNull(request.SourceReportedLandUse),
            ResearchLandCoverGroup = TrimOrNull(request.ResearchLandCoverGroup),
        };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
