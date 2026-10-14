using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetAuditHistoryByCaseQuery(string CaseId, int Limit = 100);

public sealed class GetAuditHistoryByCaseQueryHandler
{
    private readonly IGovernanceAuditCaseHistoryReader _reader;
    public GetAuditHistoryByCaseQueryHandler(IGovernanceAuditCaseHistoryReader reader) =>
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public Task<IReadOnlyList<GovernanceAuditCaseHistoryDto>> HandleAsync(
        GetAuditHistoryByCaseQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var caseId = query.CaseId?.Trim();
        if (string.IsNullOrWhiteSpace(caseId) || caseId.Length > 100)
            throw new ArgumentException("CaseId must contain between 1 and 100 characters.", nameof(query));
        if (query.Limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(query), "Limit must be between 1 and 200.");
        return _reader.GetByCaseIdAsync(caseId, query.Limit, cancellationToken);
    }
}
