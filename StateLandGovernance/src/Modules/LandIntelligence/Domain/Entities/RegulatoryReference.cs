using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Entities;

public class RegulatoryReference : Entity
{
    public string GazetteNumber { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public DateOnly EffectiveDate { get; private set; }
    public string? Summary { get; private set; }
    public AttributeProvenance? DataProvenance { get; private set; }

    private RegulatoryReference()
    {
    }

    public RegulatoryReference(
        string gazetteNumber,
        string title,
        DateOnly effectiveDate,
        string? summary = null,
        AttributeProvenance? dataProvenance = null,
        Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(gazetteNumber))
        {
            throw new ArgumentException("Gazette number is required.", nameof(gazetteNumber));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Regulatory title is required.", nameof(title));
        }

        GazetteNumber = gazetteNumber.Trim();
        Title = title.Trim();
        EffectiveDate = effectiveDate;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        DataProvenance = dataProvenance;
    }
}
