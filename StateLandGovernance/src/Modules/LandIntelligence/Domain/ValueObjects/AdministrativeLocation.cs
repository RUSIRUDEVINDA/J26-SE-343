namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record AdministrativeLocation
{
    public string Province { get; }
    public string District { get; }
    public string DivisionalSecretariat { get; }
    public string? GramaNiladhariDivision { get; }

    public AdministrativeLocation(
        string province,
        string district,
        string divisionalSecretariat,
        string? gramaNiladhariDivision = null)
    {
        if (string.IsNullOrWhiteSpace(province))
        {
            throw new ArgumentException("Province is required.", nameof(province));
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            throw new ArgumentException("District is required.", nameof(district));
        }

        if (string.IsNullOrWhiteSpace(divisionalSecretariat))
        {
            throw new ArgumentException("Divisional secretariat is required.", nameof(divisionalSecretariat));
        }

        Province = province.Trim();
        District = district.Trim();
        DivisionalSecretariat = divisionalSecretariat.Trim();
        GramaNiladhariDivision = string.IsNullOrWhiteSpace(gramaNiladhariDivision)
            ? null
            : gramaNiladhariDivision.Trim();
    }
}
