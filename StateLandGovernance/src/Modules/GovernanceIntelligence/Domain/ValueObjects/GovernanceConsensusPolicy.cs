using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public sealed record GovernanceConsensusPolicy
{
    public ConsensusType Mode { get; }
    public double RequiredPercentage { get; }
    public int? RequiredApprovalCount { get; }
    public double MinQuorumPercentage { get; }
    public bool AllowConditionalAsApproval { get; }
    public bool CountAbstentionsInQuorum { get; }
    public bool IsRejectionBlocking { get; }
    public IReadOnlyList<string> ExpectedInstitutionIds { get; }
    public IReadOnlyList<string> MandatoryInstitutionIds { get; }

    public GovernanceConsensusPolicy(
        ConsensusType mode,
        IEnumerable<string> expectedInstitutionIds,
        double requiredPercentage = 50.0,
        int? requiredApprovalCount = null,
        double minQuorumPercentage = 50.0,
        bool allowConditionalAsApproval = false,
        bool countAbstentionsInQuorum = true,
        bool isRejectionBlocking = false,
        IEnumerable<string>? mandatoryInstitutionIds = null)
    {
        var expectedList = (expectedInstitutionIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (expectedList.Count == 0)
        {
            throw new ArgumentException("Policy must define at least one expected institution ID.", nameof(expectedInstitutionIds));
        }

        var mandatoryList = (mandatoryInstitutionIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (mandatoryList.Any(m => !expectedList.Contains(m)))
        {
            throw new ArgumentException("All mandatory institution IDs must be included in expected institution IDs.", nameof(mandatoryInstitutionIds));
        }

        if (minQuorumPercentage < 0.0 || minQuorumPercentage > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(minQuorumPercentage), "Minimum quorum percentage must be between 0 and 100.");
        }

        Mode = mode;
        ExpectedInstitutionIds = expectedList.AsReadOnly();
        MandatoryInstitutionIds = mandatoryList.AsReadOnly();
        MinQuorumPercentage = minQuorumPercentage;
        AllowConditionalAsApproval = allowConditionalAsApproval;
        CountAbstentionsInQuorum = countAbstentionsInQuorum;
        IsRejectionBlocking = isRejectionBlocking;

        switch (mode)
        {
            case ConsensusType.Unanimous:
                RequiredPercentage = 100.0;
                RequiredApprovalCount = null;
                break;

            case ConsensusType.SimpleMajority:
                RequiredPercentage = 50.0;
                RequiredApprovalCount = null;
                break;

            case ConsensusType.Supermajority:
                if (requiredPercentage <= 50.0 || requiredPercentage > 100.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(requiredPercentage), "Supermajority required percentage must be greater than 50.0 and at most 100.0.");
                }
                RequiredPercentage = requiredPercentage;
                RequiredApprovalCount = null;
                break;

            case ConsensusType.ThresholdCount:
                if (!requiredApprovalCount.HasValue || requiredApprovalCount.Value <= 0 || requiredApprovalCount.Value > expectedList.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(requiredApprovalCount), $"Threshold count must be between 1 and the expected institution count ({expectedList.Count}).");
                }
                RequiredApprovalCount = requiredApprovalCount.Value;
                RequiredPercentage = 0.0;
                break;

            default:
                throw new ArgumentException($"Unsupported consensus mode: {mode}", nameof(mode));
        }
    }
}
