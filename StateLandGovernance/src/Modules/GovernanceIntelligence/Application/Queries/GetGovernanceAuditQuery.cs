using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

/// <summary>
/// Query to retrieve a governance audit record by its identifier.
/// </summary>
public sealed record GetGovernanceAuditQuery(Guid Id);

/// <summary>
/// Handler for the GetGovernanceAuditQuery.
/// </summary>
public sealed class GetGovernanceAuditQueryHandler
{
    private readonly IGovernanceAuditRepository _repository;

    public GetGovernanceAuditQueryHandler(IGovernanceAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task<GovernanceAuditRecordDto?> HandleAsync(GetGovernanceAuditQuery query, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByIdAsync(query.Id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return new GovernanceAuditRecordDto(
            record.Id,
            record.EngineType.ToString(),
            record.ActionName,
            record.Status,
            record.Details,
            record.Timestamp);
    }
}
