using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Pure domain service providing deterministic financial and economic appraisal calculations.
/// </summary>
public static class FinancialAppraisalCalculator
{
    public static FinancialAppraisalResult RecalculateNpv(
        decimal? discountRate,
        IReadOnlyList<decimal> cashFlows,
        CashFlowPeriod period,
        decimal? submittedNpv)
    {
        if (period != CashFlowPeriod.Annual)
        {
            return new FinancialAppraisalResult(
                "NPV",
                submittedNpv,
                null,
                null,
                CalculationStatus.UnsupportedInterval,
                $"Cash flow period '{period}' is not supported for NPV recalculation. Only Annual cash flows are currently supported."
            );
        }

        if (discountRate == null || cashFlows == null || cashFlows.Count == 0)
        {
            return new FinancialAppraisalResult(
                "NPV",
                submittedNpv,
                null,
                null,
                CalculationStatus.MissingInputs,
                "Missing required discount rate or cash flow series for NPV recalculation."
            );
        }

        decimal npv = 0m;
        double rate = (double)discountRate.Value;

        for (int t = 0; t < cashFlows.Count; t++)
        {
            double cf = (double)cashFlows[t];
            double discountFactor = Math.Pow(1.0 + rate, t);
            if (discountFactor == 0) continue;
            npv += (decimal)(cf / discountFactor);
        }

        npv = Math.Round(npv, 2);
        decimal? diff = submittedNpv.HasValue ? Math.Abs(submittedNpv.Value - npv) : null;

        return new FinancialAppraisalResult(
            "NPV",
            submittedNpv,
            npv,
            diff,
            CalculationStatus.Calculated,
            $"Recalculated NPV = {npv} at discount rate {discountRate.Value:P2} over {cashFlows.Count} annual periods."
        );
    }

    public static FinancialAppraisalResult CalculateIrr(
        IReadOnlyList<decimal> cashFlows,
        CashFlowPeriod period,
        decimal? submittedIrr,
        int maxIterations = 100,
        decimal tolerance = 0.000001m)
    {
        if (period != CashFlowPeriod.Annual)
        {
            return new FinancialAppraisalResult(
                "IRR",
                submittedIrr,
                null,
                null,
                CalculationStatus.UnsupportedInterval,
                $"Cash flow period '{period}' is not supported for IRR recalculation."
            );
        }

        if (cashFlows == null || cashFlows.Count < 2)
        {
            return new FinancialAppraisalResult(
                "IRR",
                submittedIrr,
                null,
                null,
                CalculationStatus.MissingInputs,
                "Insufficient cash flow series for IRR calculation (minimum 2 periods required)."
            );
        }

        // Count sign changes
        int signChanges = 0;
        for (int i = 0; i < cashFlows.Count - 1; i++)
        {
            if ((cashFlows[i] < 0 && cashFlows[i + 1] > 0) || (cashFlows[i] > 0 && cashFlows[i + 1] < 0))
            {
                signChanges++;
            }
        }

        if (signChanges == 0)
        {
            return new FinancialAppraisalResult(
                "IRR",
                submittedIrr,
                null,
                null,
                CalculationStatus.NoRootFound,
                "Cash flow series has no sign changes; IRR cannot be calculated."
            );
        }

        if (signChanges > 1)
        {
            return new FinancialAppraisalResult(
                "IRR",
                submittedIrr,
                null,
                null,
                CalculationStatus.MultipleRootsDetected,
                $"Cash flow series has {signChanges} sign changes (non-conventional cash flow); multiple internal rates of return exist."
            );
        }

        // Newton-Raphson solver
        double rate = 0.10; // initial guess 10%
        bool converged = false;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double f = 0.0;
            double df = 0.0;

            for (int t = 0; t < cashFlows.Count; t++)
            {
                double cf = (double)cashFlows[t];
                double factor = Math.Pow(1.0 + rate, t);
                f += cf / factor;
                if (t > 0)
                {
                    df -= t * cf / (factor * (1.0 + rate));
                }
            }

            if (Math.Abs(df) < 1e-12) break;

            double nextRate = rate - f / df;

            if (Math.Abs(nextRate - rate) < (double)tolerance)
            {
                rate = nextRate;
                converged = true;
                break;
            }

            rate = nextRate;
            if (rate <= -0.99 || rate > 10.0) break; // Out of reasonable bounds
        }

        // Fallback to Bisection if Newton-Raphson failed
        if (!converged)
        {
            double low = -0.99;
            double high = 5.0;

            double fLow = CalculateNpvDouble(cashFlows, low);
            double fHigh = CalculateNpvDouble(cashFlows, high);

            if (fLow * fHigh <= 0)
            {
                for (int iter = 0; iter < maxIterations; iter++)
                {
                    double mid = (low + high) / 2.0;
                    double fMid = CalculateNpvDouble(cashFlows, mid);

                    if (Math.Abs(fMid) < (double)tolerance || (high - low) / 2.0 < (double)tolerance)
                    {
                        rate = mid;
                        converged = true;
                        break;
                    }

                    if (fLow * fMid < 0)
                    {
                        high = mid;
                        fHigh = fMid;
                    }
                    else
                    {
                        low = mid;
                        fLow = fMid;
                    }
                }
            }
        }

        if (!converged)
        {
            return new FinancialAppraisalResult(
                "IRR",
                submittedIrr,
                null,
                null,
                CalculationStatus.NonConvergent,
                "IRR calculation failed to converge within deterministic iteration limit."
            );
        }

        decimal calculatedIrr = Math.Round((decimal)rate, 4);
        decimal? diff = submittedIrr.HasValue ? Math.Abs(submittedIrr.Value - calculatedIrr) : null;

        return new FinancialAppraisalResult(
            "IRR",
            submittedIrr,
            calculatedIrr,
            diff,
            CalculationStatus.Calculated,
            $"Calculated IRR = {calculatedIrr:P2} (converged deterministically)."
        );
    }

    public static FinancialAppraisalResult CalculatePaybackPeriod(
        IReadOnlyList<decimal> cashFlows,
        CashFlowPeriod period,
        decimal? submittedPayback)
    {
        if (period != CashFlowPeriod.Annual)
        {
            return new FinancialAppraisalResult(
                "PaybackPeriod",
                submittedPayback,
                null,
                null,
                CalculationStatus.UnsupportedInterval,
                $"Cash flow period '{period}' is not supported for Payback Period calculation."
            );
        }

        if (cashFlows == null || cashFlows.Count == 0 || cashFlows[0] >= 0)
        {
            return new FinancialAppraisalResult(
                "PaybackPeriod",
                submittedPayback,
                null,
                null,
                CalculationStatus.MissingInputs,
                "Initial cash flow must be negative (investment outlay) to calculate payback period."
            );
        }

        decimal cumulative = 0m;
        decimal? paybackYears = null;

        for (int t = 0; t < cashFlows.Count; t++)
        {
            decimal prevCumulative = cumulative;
            cumulative += cashFlows[t];

            if (cumulative >= 0 && prevCumulative < 0)
            {
                // Interpolate fractional year
                decimal required = Math.Abs(prevCumulative);
                decimal cfCurrent = cashFlows[t];
                decimal fraction = cfCurrent > 0 ? required / cfCurrent : 0m;
                paybackYears = (t - 1) + fraction;
                break;
            }
        }

        if (paybackYears == null)
        {
            return new FinancialAppraisalResult(
                "PaybackPeriod",
                submittedPayback,
                null,
                null,
                CalculationStatus.NoRootFound,
                "Investment is not fully recovered within the supplied cash flow series."
            );
        }

        paybackYears = Math.Round(paybackYears.Value, 2);
        decimal? diff = submittedPayback.HasValue ? Math.Abs(submittedPayback.Value - paybackYears.Value) : null;

        return new FinancialAppraisalResult(
            "PaybackPeriod",
            submittedPayback,
            paybackYears,
            diff,
            CalculationStatus.Calculated,
            $"Calculated Payback Period = {paybackYears.Value} years."
        );
    }

    public static FinancialAppraisalResult CalculateCostBenefitRatio(
        decimal? pvBenefits,
        decimal? pvCosts,
        decimal? submittedCbr,
        CostBenefitRatioConvention convention)
    {
        if (convention == CostBenefitRatioConvention.Unresolved)
        {
            return new FinancialAppraisalResult(
                "CostBenefitRatio",
                submittedCbr,
                null,
                null,
                CalculationStatus.RequiresStakeholderConfirmation,
                "Cost-Benefit Ratio numerator/denominator convention is unresolved. Stakeholder confirmation required before calculating or evaluating regulatory acceptance."
            );
        }

        if (pvBenefits == null || pvCosts == null || pvCosts == 0)
        {
            return new FinancialAppraisalResult(
                "CostBenefitRatio",
                submittedCbr,
                null,
                null,
                CalculationStatus.MissingInputs,
                "Missing PV of Benefits or PV of Costs required for Cost-Benefit Ratio calculation."
            );
        }

        decimal calculatedRatio;
        if (convention == CostBenefitRatioConvention.BenefitsOverCosts)
        {
            calculatedRatio = Math.Round(pvBenefits.Value / pvCosts.Value, 4);
        }
        else // CostsOverBenefits
        {
            calculatedRatio = Math.Round(pvCosts.Value / pvBenefits.Value, 4);
        }

        decimal? diff = submittedCbr.HasValue ? Math.Abs(submittedCbr.Value - calculatedRatio) : null;

        return new FinancialAppraisalResult(
            "CostBenefitRatio",
            submittedCbr,
            calculatedRatio,
            diff,
            CalculationStatus.RequiresStakeholderConfirmation,
            $"Calculated Cost-Benefit Ratio = {calculatedRatio} (Convention: {convention}). Regulatory pass/fail acceptance requires official stakeholder threshold confirmation."
        );
    }

    private static double CalculateNpvDouble(IReadOnlyList<decimal> cashFlows, double rate)
    {
        double npv = 0.0;
        for (int t = 0; t < cashFlows.Count; t++)
        {
            npv += (double)cashFlows[t] / Math.Pow(1.0 + rate, t);
        }
        return npv;
    }
}
