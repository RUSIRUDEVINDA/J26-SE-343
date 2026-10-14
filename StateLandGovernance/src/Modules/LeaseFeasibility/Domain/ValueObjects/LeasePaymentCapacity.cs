using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

/// <summary>Research affordability rule, separate from the scoring bands.</summary>
public sealed record LeasePaymentCapacity
{
    public string RuleVersion => "component-2-monthly-payment-capacity-60-v1";
    public decimal MaximumDebtToIncomeRatio => 0.60m;
    public decimal VerifiedMonthlyIncomeLkr { get; }
    public decimal ExistingMonthlyDebtPaymentsLkr { get; }
    public decimal RequestedMonthlyLeasePaymentLkr { get; }
    public decimal MaximumTotalMonthlyPaymentsLkr { get; }
    public decimal AvailableMonthlyLeasePaymentLkr { get; }
    public decimal ExistingDebtExcessLkr { get; }
    public decimal ProposedPaymentExcessLkr { get; }
    public bool IsWithinLimit => ProposedPaymentExcessLkr == 0m;

    public LeasePaymentCapacity(decimal income, decimal existingDebt, decimal requestedLease)
    {
        if (income <= 0m) throw new ArgumentOutOfRangeException(nameof(income));
        if (existingDebt < 0m) throw new ArgumentOutOfRangeException(nameof(existingDebt));
        if (requestedLease <= 0m) throw new ArgumentOutOfRangeException(nameof(requestedLease));
        VerifiedMonthlyIncomeLkr = income;
        ExistingMonthlyDebtPaymentsLkr = existingDebt;
        RequestedMonthlyLeasePaymentLkr = requestedLease;
        // Floor to cents so rounding never authorizes a payment above 60%.
        MaximumTotalMonthlyPaymentsLkr = Math.Floor(income * 0.60m * 100m) / 100m;
        AvailableMonthlyLeasePaymentLkr = Math.Max(0m, MaximumTotalMonthlyPaymentsLkr - existingDebt);
        ExistingDebtExcessLkr = Math.Max(0m, existingDebt - MaximumTotalMonthlyPaymentsLkr);
        ProposedPaymentExcessLkr = Math.Max(0m, existingDebt + requestedLease - MaximumTotalMonthlyPaymentsLkr);
    }
}
