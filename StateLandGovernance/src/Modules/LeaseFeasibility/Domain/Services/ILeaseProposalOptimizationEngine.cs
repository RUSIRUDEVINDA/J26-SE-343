using System;
using StateLandGovernance.LeaseFeasibility.Domain.Entities;

namespace StateLandGovernance.LeaseFeasibility.Domain.Services;

/// <summary>
/// Domain service contract for optimizing lease proposals based on historical success data.
/// </summary>
public interface ILeaseProposalOptimizationEngine
{
    /// <summary>
    /// Optimizes and generates a lease proposal to maximize approval likelihood.
    /// </summary>
    /// <param name="input">The proposal context input data.</param>
    /// <param name="optimizationTimestamp">UTC optimization timestamp.</param>
    /// <returns>An optimized lease proposal.</returns>
    LeaseProposal OptimizeProposal(object input, DateTime optimizationTimestamp);
}
