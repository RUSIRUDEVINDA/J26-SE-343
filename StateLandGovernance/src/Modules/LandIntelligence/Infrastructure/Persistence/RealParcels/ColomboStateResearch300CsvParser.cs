using System.Text;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

internal static class ColomboStateResearch300CsvParser
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = false;
                    continue;
                }

                field.Append(c);
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                if (row.Count > 1 || row[0].Length > 0)
                {
                    rows.Add(row);
                }

                row = new List<string>();
                field.Clear();
                continue;
            }

            field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    public static Dictionary<string, int> ReadHeader(IReadOnlyList<string> headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headerRow.Count; i++)
        {
            var name = headerRow[i].Trim().TrimStart('\uFEFF');
            if (name.Length > 0)
            {
                map[name] = i;
            }
        }

        return map;
    }

    public static string GetField(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndex,
        string columnName)
    {
        if (!columnIndex.TryGetValue(columnName, out var index) || index >= row.Count)
        {
            return string.Empty;
        }

        return row[index].Trim();
    }
}
