using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

public interface IGovernanceAuditCaseHistoryReader
{
    Task<IReadOnlyList<GovernanceAuditCaseHistoryDto>> GetByCaseIdAsync(
        string caseId, int limit, CancellationToken cancellationToken = default);
}
