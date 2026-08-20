using System;
using System.Reflection;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

/// <summary>
/// Bidirectional mapper between Domain entity GovernanceAuditRecord and Infrastructure persistence entity GovernanceAuditRecordEntity.
/// </summary>
internal static class GovernanceAuditRecordMapper
{
    private static readonly ConstructorInfo DomainConstructor = typeof(GovernanceAuditRecord).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new[] { typeof(Guid), typeof(EngineType), typeof(string), typeof(string), typeof(string), typeof(DateTime) },
        null) ?? throw new InvalidOperationException("Private constructor for GovernanceAuditRecord not found.");

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
            Timestamp = domain.Timestamp
        };
    }

    public static GovernanceAuditRecord ToDomain(GovernanceAuditRecordEntity entity)
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        return (GovernanceAuditRecord)DomainConstructor.Invoke(new object[]
        {
            entity.Id,
            (EngineType)entity.EngineType,
            entity.ActionName,
            entity.Status,
            entity.Details,
            entity.Timestamp
        });
    }
}
