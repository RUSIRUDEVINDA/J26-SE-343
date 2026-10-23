using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.RealParcels;

public static class Colombo400GnDivisionMappingCsv
{
    public const string ExpectedWorkbookSha256 =
        "ea8d275e0e42f7918fee704762036198a67b9d9a47a3204fc680ffa83be2a464";

    public const int ExpectedParcelCount = 400;

    public static IReadOnlyList<Colombo400GnDivisionMappingRow> Parse(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new InvalidOperationException("GN division mapping CSV is empty.");
        }

        using var reader = new StringReader(csvText);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            throw new InvalidOperationException("GN division mapping CSV has no header row.");
        }

        var headers = SplitCsvLine(headerLine);
        var columnIndex = BuildColumnIndex(headers);
        var rows = new List<Colombo400GnDivisionMappingRow>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = SplitCsvLine(line);
            rows.Add(ReadRow(fields, columnIndex));
        }

        return rows;
    }

    public static Colombo400GnDivisionMappingValidation Validate(IReadOnlyList<Colombo400GnDivisionMappingRow> rows)
    {
        var cadastralNumbers = rows.Select(row => row.CadastralNumber).ToList();
        var duplicateGroups = cadastralNumbers
            .GroupBy(number => number, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        var statusCounts = rows
            .GroupBy(row => row.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        return new Colombo400GnDivisionMappingValidation(
            rows.Count,
            cadastralNumbers.Distinct(StringComparer.Ordinal).Count(),
            duplicateGroups,
            statusCounts);
    }

    public static string ComputeSha256(string csvText)
    {
        var bytes = Encoding.UTF8.GetBytes(csvText);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static ParcelGnDivisionMappingStatus ParseWorkbookStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "Source code and map marker agree" => ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree,
            "Source code and map marker differ - review" =>
                ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerDifferReview,
            "Source code outside Colombo - review" =>
                ParcelGnDivisionMappingStatus.SourceCodeOutsideColomboReview,
            "Missing source GN code - review" =>
                ParcelGnDivisionMappingStatus.MissingSourceGnCodeReview,
            _ => throw new InvalidOperationException($"Unknown GN mapping status label: {value}"),
        };
    }

    public static string WorkbookStatusLabel(ParcelGnDivisionMappingStatus status) =>
        status switch
        {
            ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree =>
                "Source code and map marker agree",
            ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerDifferReview =>
                "Source code and map marker differ - review",
            ParcelGnDivisionMappingStatus.SourceCodeOutsideColomboReview =>
                "Source code outside Colombo - review",
            ParcelGnDivisionMappingStatus.MissingSourceGnCodeReview =>
                "Missing source GN code - review",
            _ => "Unknown",
        };

    private static Colombo400GnDivisionMappingRow ReadRow(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columnIndex)
    {
        var cadastralNumber = NormalizeIdentifier(Field(fields, columnIndex, "Cadastral number"));
        if (cadastralNumber.Length == 0)
        {
            throw new InvalidOperationException("GN mapping row is missing cadastral number.");
        }

        var workbookStatus = Field(fields, columnIndex, "Mapping status");
        var status = ParseWorkbookStatus(workbookStatus);

        return new Colombo400GnDivisionMappingRow(
            DatasetKey: Field(fields, columnIndex, "Dataset"),
            CadastralNumber: cadastralNumber,
            SourceGnIndex: NormalizeIdentifier(Field(fields, columnIndex, "Source GN index")),
            SourceCodeGnName: NullIfEmpty(Field(fields, columnIndex, "Name from source code")),
            SourceCodeGnNumber: NullIfEmpty(Field(fields, columnIndex, "Source GN no.")),
            SourceCodeDs: NullIfEmpty(Field(fields, columnIndex, "Source DS")),
            CodeDistrict: NullIfEmpty(Field(fields, columnIndex, "Code district")),
            MarkerGnName: NullIfEmpty(Field(fields, columnIndex, "GN at map marker")),
            MarkerGnNumber: NullIfEmpty(Field(fields, columnIndex, "GN no. at marker")),
            MarkerDs: NullIfEmpty(Field(fields, columnIndex, "DS at marker")),
            Status: status,
            WorkbookStatusLabel: workbookStatus.Trim(),
            MarkerLatitude: ParseOptionalDouble(Field(fields, columnIndex, "Marker latitude")),
            MarkerLongitude: ParseOptionalDouble(Field(fields, columnIndex, "Marker longitude")));
    }

    private static IReadOnlyDictionary<string, int> BuildColumnIndex(IReadOnlyList<string> headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var name = headers[i]?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                index[name] = i;
            }
        }

        return index;
    }

    private static string Field(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columnIndex, string name)
    {
        if (!columnIndex.TryGetValue(name, out var index) || index >= fields.Count)
        {
            return string.Empty;
        }

        return fields[index] ?? string.Empty;
    }

    private static string NormalizeIdentifier(string value) => value.Trim();

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static double? ParseOptionalDouble(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }
}

public sealed record Colombo400GnDivisionMappingRow(
    string DatasetKey,
    string CadastralNumber,
    string SourceGnIndex,
    string? SourceCodeGnName,
    string? SourceCodeGnNumber,
    string? SourceCodeDs,
    string? CodeDistrict,
    string? MarkerGnName,
    string? MarkerGnNumber,
    string? MarkerDs,
    ParcelGnDivisionMappingStatus Status,
    string WorkbookStatusLabel,
    double? MarkerLatitude,
    double? MarkerLongitude);

public sealed record Colombo400GnDivisionMappingValidation(
    int RowCount,
    int DistinctCadastralCount,
    IReadOnlyList<string> DuplicateCadastralNumbers,
    IReadOnlyDictionary<ParcelGnDivisionMappingStatus, int> StatusCounts)
{
    public bool IsValid =>
        RowCount == Colombo400GnDivisionMappingCsv.ExpectedParcelCount
        && DistinctCadastralCount == Colombo400GnDivisionMappingCsv.ExpectedParcelCount
        && DuplicateCadastralNumbers.Count == 0
        && StatusCounts.GetValueOrDefault(ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree) == 356
        && StatusCounts.GetValueOrDefault(ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerDifferReview) == 19
        && StatusCounts.GetValueOrDefault(ParcelGnDivisionMappingStatus.SourceCodeOutsideColomboReview) == 24
        && StatusCounts.GetValueOrDefault(ParcelGnDivisionMappingStatus.MissingSourceGnCodeReview) == 1;
}
