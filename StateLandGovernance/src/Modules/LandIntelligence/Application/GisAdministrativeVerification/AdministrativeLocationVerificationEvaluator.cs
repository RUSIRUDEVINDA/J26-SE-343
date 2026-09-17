using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public static class AdministrativeLocationVerificationEvaluator
{
    public static AdministrativeLocationVerificationStatus ResolveStatus(
        string? detectedDistrict,
        string? detectedProvince,
        bool districtMatches,
        bool provinceMatches)
    {
        if (string.IsNullOrWhiteSpace(detectedDistrict) || string.IsNullOrWhiteSpace(detectedProvince))
        {
            return AdministrativeLocationVerificationStatus.Unavailable;
        }

        if (districtMatches && provinceMatches)
        {
            return AdministrativeLocationVerificationStatus.Verified;
        }

        return AdministrativeLocationVerificationStatus.Mismatch;
    }

    public static (bool? DistrictMatches, bool? ProvinceMatches) CompareLocations(
        string storedDistrict,
        string storedProvince,
        string? detectedDistrict,
        string? detectedProvince)
    {
        if (string.IsNullOrWhiteSpace(detectedDistrict) || string.IsNullOrWhiteSpace(detectedProvince))
        {
            return (null, null);
        }

        return (
            AdministrativeNameNormalizer.NamesMatch(storedDistrict, detectedDistrict),
            AdministrativeNameNormalizer.NamesMatch(storedProvince, detectedProvince));
    }
}
