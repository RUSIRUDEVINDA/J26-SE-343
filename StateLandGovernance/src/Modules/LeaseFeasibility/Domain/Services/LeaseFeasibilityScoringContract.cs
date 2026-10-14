using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// The complete, versioned Component 2 deterministic scoring contract.
/// Thresholds are ratios, liquidity values are months, monetary inputs are LKR per month,
/// and all score contributions are points.
/// </summary>
public sealed record LeaseFeasibilityScoringContract(
    string Version,
    string CurrencyCode,
    int EvidenceWindowMonths,
    decimal StrongDebtServiceRatioMaximum,
    decimal ManageableDebtServiceRatioMaximum,
    decimal MarginalDebtServiceRatioMaximum,
    decimal StrongDebtServiceRatioPoints,
    decimal ManageableDebtServiceRatioPoints,
    decimal MarginalDebtServiceRatioPoints,
    decimal DebtServiceRatioWeight,
    decimal IncomeConsistencyWeight,
    decimal FullLiquidityBufferMonths,
    decimal AdequateLiquidityBufferMonths,
    decimal MinimumLiquidityBufferMonths,
    decimal FullLiquidityBufferPoints,
    decimal AdequateLiquidityBufferPoints,
    decimal MinimumLiquidityBufferPoints,
    decimal LiquidityBufferWeight,
    decimal CreditGradeAPoints,
    decimal CreditGradeBPoints,
    decimal CreditGradeCPoints,
    decimal CreditHistoryWeight,
    decimal DefaultHistoryPenalty,
    int FrequentOverdraftThreshold,
    decimal FrequentOverdraftPenalty,
    decimal GradeAThreshold,
    decimal GradeBThreshold,
    decimal GradeCThreshold,
    decimal GradeDThreshold,
    FeasibilityAction GradeAAction,
    FeasibilityAction GradeBAction,
    FeasibilityAction GradeCAction,
    FeasibilityAction GradeDAction,
    FeasibilityAction GradeEAction)
{
    public static LeaseFeasibilityScoringContract Component2V1 { get; } = new(
        Version: "component-2-financial-feasibility-v1",
        CurrencyCode: "LKR",
        EvidenceWindowMonths: 6,
        StrongDebtServiceRatioMaximum: 0.20m,
        ManageableDebtServiceRatioMaximum: 0.35m,
        MarginalDebtServiceRatioMaximum: 0.50m,
        StrongDebtServiceRatioPoints: 35m,
        ManageableDebtServiceRatioPoints: 25m,
        MarginalDebtServiceRatioPoints: 10m,
        DebtServiceRatioWeight: 35m,
        IncomeConsistencyWeight: 25m,
        FullLiquidityBufferMonths: 6m,
        AdequateLiquidityBufferMonths: 3m,
        MinimumLiquidityBufferMonths: 1m,
        FullLiquidityBufferPoints: 20m,
        AdequateLiquidityBufferPoints: 15m,
        MinimumLiquidityBufferPoints: 5m,
        LiquidityBufferWeight: 20m,
        CreditGradeAPoints: 20m,
        CreditGradeBPoints: 15m,
        CreditGradeCPoints: 5m,
        CreditHistoryWeight: 20m,
        DefaultHistoryPenalty: -50m,
        FrequentOverdraftThreshold: 3,
        FrequentOverdraftPenalty: -15m,
        GradeAThreshold: 85m,
        GradeBThreshold: 70m,
        GradeCThreshold: 50m,
        GradeDThreshold: 30m,
        GradeAAction: FeasibilityAction.FastTrack,
        GradeBAction: FeasibilityAction.Proceed,
        GradeCAction: FeasibilityAction.ManualReview,
        GradeDAction: FeasibilityAction.Escalate,
        GradeEAction: FeasibilityAction.Reject);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version)) throw new InvalidOperationException("Scoring contract version is required.");
        if (!string.Equals(CurrencyCode, "LKR", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Component 2 V1 monetary inputs must use LKR.");
        }
        if (EvidenceWindowMonths <= 0) throw new InvalidOperationException("Evidence window must be positive.");

        if (!(0m < StrongDebtServiceRatioMaximum &&
              StrongDebtServiceRatioMaximum < ManageableDebtServiceRatioMaximum &&
              ManageableDebtServiceRatioMaximum < MarginalDebtServiceRatioMaximum))
        {
            throw new InvalidOperationException("Debt-service ratio thresholds must be positive and strictly increasing.");
        }

        if (!(0m < MinimumLiquidityBufferMonths &&
              MinimumLiquidityBufferMonths < AdequateLiquidityBufferMonths &&
              AdequateLiquidityBufferMonths < FullLiquidityBufferMonths))
        {
            throw new InvalidOperationException("Liquidity thresholds must be positive and strictly increasing.");
        }

        if (DebtServiceRatioWeight + IncomeConsistencyWeight + LiquidityBufferWeight + CreditHistoryWeight != 100m)
        {
            throw new InvalidOperationException("Positive criterion weights must total 100 points.");
        }

        ValidatePoints(StrongDebtServiceRatioPoints, DebtServiceRatioWeight, nameof(StrongDebtServiceRatioPoints));
        ValidatePoints(ManageableDebtServiceRatioPoints, DebtServiceRatioWeight, nameof(ManageableDebtServiceRatioPoints));
        ValidatePoints(MarginalDebtServiceRatioPoints, DebtServiceRatioWeight, nameof(MarginalDebtServiceRatioPoints));
        ValidatePoints(FullLiquidityBufferPoints, LiquidityBufferWeight, nameof(FullLiquidityBufferPoints));
        ValidatePoints(AdequateLiquidityBufferPoints, LiquidityBufferWeight, nameof(AdequateLiquidityBufferPoints));
        ValidatePoints(MinimumLiquidityBufferPoints, LiquidityBufferWeight, nameof(MinimumLiquidityBufferPoints));
        ValidatePoints(CreditGradeAPoints, CreditHistoryWeight, nameof(CreditGradeAPoints));
        ValidatePoints(CreditGradeBPoints, CreditHistoryWeight, nameof(CreditGradeBPoints));
        ValidatePoints(CreditGradeCPoints, CreditHistoryWeight, nameof(CreditGradeCPoints));

        if (StrongDebtServiceRatioPoints != DebtServiceRatioWeight ||
            FullLiquidityBufferPoints != LiquidityBufferWeight ||
            CreditGradeAPoints != CreditHistoryWeight)
        {
            throw new InvalidOperationException("Each best-performing tier must award its full criterion weight.");
        }

        if (!(StrongDebtServiceRatioPoints > ManageableDebtServiceRatioPoints &&
              ManageableDebtServiceRatioPoints > MarginalDebtServiceRatioPoints) ||
            !(FullLiquidityBufferPoints > AdequateLiquidityBufferPoints &&
              AdequateLiquidityBufferPoints > MinimumLiquidityBufferPoints) ||
            !(CreditGradeAPoints > CreditGradeBPoints && CreditGradeBPoints > CreditGradeCPoints))
        {
            throw new InvalidOperationException("Tier points must decrease as financial strength decreases.");
        }

        if (DefaultHistoryPenalty > 0m || FrequentOverdraftPenalty > 0m)
        {
            throw new InvalidOperationException("Penalties cannot be positive.");
        }

        if (FrequentOverdraftThreshold < 0)
        {
            throw new InvalidOperationException("Overdraft threshold cannot be negative.");
        }

        if (!(0m <= GradeDThreshold && GradeDThreshold < GradeCThreshold &&
              GradeCThreshold < GradeBThreshold && GradeBThreshold < GradeAThreshold &&
              GradeAThreshold <= 100m))
        {
            throw new InvalidOperationException("Grade thresholds must be ordered D < C < B < A within 0-100.");
        }

        if (!Enum.IsDefined(GradeAAction) || !Enum.IsDefined(GradeBAction) ||
            !Enum.IsDefined(GradeCAction) || !Enum.IsDefined(GradeDAction) ||
            !Enum.IsDefined(GradeEAction))
        {
            throw new InvalidOperationException("Every grade must map to a defined action.");
        }
    }

    public FeasibilityGrade DeriveGrade(decimal score)
    {
        if (score is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");
        }

        if (score >= GradeAThreshold) return FeasibilityGrade.A;
        if (score >= GradeBThreshold) return FeasibilityGrade.B;
        if (score >= GradeCThreshold) return FeasibilityGrade.C;
        if (score >= GradeDThreshold) return FeasibilityGrade.D;
        return FeasibilityGrade.E;
    }

    public FeasibilityAction DeriveAction(FeasibilityGrade grade) => grade switch
    {
        FeasibilityGrade.A => GradeAAction,
        FeasibilityGrade.B => GradeBAction,
        FeasibilityGrade.C => GradeCAction,
        FeasibilityGrade.D => GradeDAction,
        FeasibilityGrade.E => GradeEAction,
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unknown feasibility grade.")
    };

    private static void ValidatePoints(decimal points, decimal maximum, string name)
    {
        if (points is < 0m || points > maximum)
        {
            throw new InvalidOperationException($"{name} must be between zero and its criterion weight.");
        }
    }
}
