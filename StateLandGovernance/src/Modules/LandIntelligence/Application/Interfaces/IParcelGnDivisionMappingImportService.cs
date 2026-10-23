namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IParcelGnDivisionMappingImportService
{
    Task<ParcelGnDivisionMappingImportReport> EnsureImportedAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ParcelGnDivisionMappingImportReport(
    string Mode,
    int RowsRead,
    int Upserted,
    int ParcelsLinked,
    int UnlinkedCadastralNumbers,
    string WorkbookSha256,
    DateTimeOffset CompletedAtUtc);
