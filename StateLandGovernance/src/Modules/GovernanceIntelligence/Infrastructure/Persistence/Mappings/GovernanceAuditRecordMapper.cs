using System;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

/// <summary>
/// Bidirectional mapper between Domain entity GovernanceAuditRecord and Infrastructure persistence entity GovernanceAuditRecordEntity.
/// </summary>
internal static class GovernanceAuditRecordMapper
{
    public static GovernanceAuditRecordEntity ToEntity(GovernanceAuditRecord domain)
    {
        if (domain is null)
        {
            throw new ArgumentNullException(nameof(domain));
        }

        return new GovernanceAuditRecordEntity
        {
            Id = domain.Id,
            EngineType = (int)domain.EngineType,
            ActionName = domain.ActionName,
            Status = domain.Status,
            Details = domain.Details,
            Timestamp = domain.Timestamp,
            HashVersion = domain.HashVersion
        };
    }

    public static GovernanceAuditRecord ToDomain(GovernanceAuditRecordEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        return GovernanceAuditRecord.Rehydrate(
            entity.Id,
            (EngineType)entity.EngineType,
            entity.ActionName,
            entity.Status,
            entity.Details,
            entity.Timestamp,
            entity.HashVersion);
    }
}
