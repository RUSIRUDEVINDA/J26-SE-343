namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;

public readonly struct HumanFactVerificationId : IEquatable<HumanFactVerificationId>
{
    public Guid Value { get; }

    public HumanFactVerificationId(Guid value)
    {
        Value = value;
    }

    public bool Equals(HumanFactVerificationId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is HumanFactVerificationId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(HumanFactVerificationId left, HumanFactVerificationId right) => left.Equals(right);
    public static bool operator !=(HumanFactVerificationId left, HumanFactVerificationId right) => !left.Equals(right);
}
