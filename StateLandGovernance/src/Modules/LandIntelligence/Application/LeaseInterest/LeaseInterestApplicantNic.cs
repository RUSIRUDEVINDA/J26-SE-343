using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

public static class LeaseInterestApplicantNic
{
    public static string? SelectRawNic(LeaseInterestApplicantDto applicant) =>
        applicant.Kind == LeaseInterestApplicantKindDto.Individual
            ? applicant.NationalIdentityCardNumber
            : applicant.RepresentativeNationalIdentityCardNumber;

    public static LeaseInterestApplicantDto WithNicFields(
        LeaseInterestApplicantDto applicant,
        string? individualNic,
        string? representativeNic) =>
        applicant with
        {
            NationalIdentityCardNumber = individualNic,
            RepresentativeNationalIdentityCardNumber = representativeNic,
        };

    public static LeaseInterestApplicantDto ClearNicFromApplicant(LeaseInterestApplicantDto applicant) =>
        applicant with
        {
            NationalIdentityCardNumber = null,
            RepresentativeNationalIdentityCardNumber = null,
        };

    public static LeaseInterestApplicantDto MergeDecryptedNic(
        LeaseInterestApplicantDto applicant,
        string? decryptedNormalizedNic)
    {
        if (string.IsNullOrWhiteSpace(decryptedNormalizedNic))
        {
            return applicant;
        }

        return applicant.Kind == LeaseInterestApplicantKindDto.Individual
            ? applicant with { NationalIdentityCardNumber = decryptedNormalizedNic }
            : applicant with { RepresentativeNationalIdentityCardNumber = decryptedNormalizedNic };
    }

    public static LeaseInterestApplicantDto MaskForApiResponse(LeaseInterestApplicantDto applicant) =>
        applicant with
        {
            NationalIdentityCardNumber = SriLankanNic.Mask(applicant.NationalIdentityCardNumber),
            RepresentativeNationalIdentityCardNumber = SriLankanNic.Mask(
                applicant.RepresentativeNationalIdentityCardNumber),
        };
}
