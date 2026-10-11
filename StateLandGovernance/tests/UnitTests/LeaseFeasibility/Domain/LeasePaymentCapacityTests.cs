using System;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Domain;

public class LeasePaymentCapacityTests
{
    [Theory]
    [InlineData(45000, 15000, 0)]
    [InlineData(60000, 0, 0)]
    [InlineData(70000, 0, 10000)]
    public void Capacity_SubtractsMonthlyPaymentsAndNeverBecomesNegative(int debt, int capacity, int debtExcess)
    {
        var result = new LeasePaymentCapacity(100000m, debt, 15000m);
        Assert.Equal(60000m, result.MaximumTotalMonthlyPaymentsLkr);
        Assert.Equal(capacity, result.AvailableMonthlyLeasePaymentLkr);
        Assert.Equal(debtExcess, result.ExistingDebtExcessLkr);
    }

    [Fact]
    public void Capacity_FloorsBudgetToCentsRatherThanAuthorizeAnOverage()
    {
        var result = new LeasePaymentCapacity(100000.01m, 45000m, 15000.01m);
        Assert.Equal(60000m, result.MaximumTotalMonthlyPaymentsLkr);
        Assert.False(result.IsWithinLimit);
        Assert.Equal(0.01m, result.ProposedPaymentExcessLkr);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(100, -1, 1)]
    [InlineData(100, 0, 0)]
    public void Capacity_RejectsInvalidInputs(int income, int debt, int lease)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LeasePaymentCapacity(income, debt, lease));
}
