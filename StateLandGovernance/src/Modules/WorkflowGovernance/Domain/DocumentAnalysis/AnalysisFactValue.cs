namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Globalization;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisFactValue : IEquatable<AnalysisFactValue>
{
    public AnalysisFactValueKind Kind { get; }
    public string CanonicalValue { get; }

    public AnalysisFactValue(AnalysisFactValueKind kind, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidAnalysisFactValueException("Value required.");
        var trimmed = value.Trim();
        if (trimmed.Length > 1000) throw new InvalidAnalysisFactValueException("Value too long.");

        foreach(char c in trimmed)
        {
            if (char.IsControl(c)) throw new InvalidAnalysisFactValueException("Control chars not allowed.");
        }

        switch (kind)
        {
            case AnalysisFactValueKind.Text:
                if (string.IsNullOrEmpty(trimmed)) throw new InvalidAnalysisFactValueException("Text cannot be empty.");
                break;
            case AnalysisFactValueKind.Identifier:
                if (string.IsNullOrEmpty(trimmed)) throw new InvalidAnalysisFactValueException("Identifier cannot be empty.");
                if (trimmed.Any(char.IsControl)) throw new InvalidAnalysisFactValueException("Control characters not allowed.");
                break;
            case AnalysisFactValueKind.Integer:
                if (!long.TryParse(trimmed, NumberStyles.None | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long lval))
                    throw new InvalidAnalysisFactValueException("Invalid integer.");
                if (lval.ToString(CultureInfo.InvariantCulture) != trimmed)
                    throw new InvalidAnalysisFactValueException("Non-canonical integer.");
                break;
            case AnalysisFactValueKind.Decimal:
                if (!decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal dval))
                    throw new InvalidAnalysisFactValueException("Invalid decimal.");
                var canon = dval.ToString("G29", CultureInfo.InvariantCulture);
                if (canon != trimmed) throw new InvalidAnalysisFactValueException("Non-canonical decimal.");
                break;
            case AnalysisFactValueKind.Date:
                if (!DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    throw new InvalidAnalysisFactValueException("Invalid date.");
                break;
            case AnalysisFactValueKind.Boolean:
                if (trimmed != "true" && trimmed != "false") throw new InvalidAnalysisFactValueException("Invalid boolean.");
                break;
            default:
                throw new InvalidAnalysisFactValueException("Invalid kind.");
        }

        Kind = kind;
        CanonicalValue = trimmed;
    }
    public bool Equals(AnalysisFactValue? other)
    {
        if (other is null) return false;
        return Kind == other.Kind && string.Equals(CanonicalValue, other.CanonicalValue, StringComparison.Ordinal);
    }
    public override bool Equals(object? obj) => Equals(obj as AnalysisFactValue);
    public override int GetHashCode() => HashCode.Combine(Kind, CanonicalValue);
}
