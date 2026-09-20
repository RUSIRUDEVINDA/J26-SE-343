using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of the regulatory compliance rule catalogue provider.
/// Loads active regulatory rules, sources, and parameters from the PostgreSQL database.
/// </summary>
public sealed class PostgresComplianceRuleCatalogue : IComplianceRuleCatalogue
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresComplianceRuleCatalogue(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ComplianceRuleDefinition>> GetActiveRulesAsync(
        DateTime evaluationTimestamp,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ComplianceRules
            .Include(r => r.Source)
            .Include(r => r.Parameters)
            .Where(r => r.Enabled &&
                        (r.EffectiveFrom == null || r.EffectiveFrom <= evaluationTimestamp) &&
                        (r.EffectiveTo == null || r.EffectiveTo >= evaluationTimestamp))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            throw new InvalidOperationException("PostgreSQL regulatory rule catalogue is empty or unavailable.");
        }

        // Check for ambiguous multiple active versions per RuleCode
        var ambiguousGroups = entities
            .GroupBy(r => r.RuleCode)
            .Where(g => g.Count() > 1)
            .ToList();

        if (ambiguousGroups.Count > 0)
        {
            var ambiguousCodes = string.Join(", ", ambiguousGroups.Select(g => g.Key));
            throw new InvalidOperationException($"Ambiguous active rule versions detected for RuleCode(s): {ambiguousCodes}.");
        }

        var definitions = new List<ComplianceRuleDefinition>();

        foreach (var entity in entities)
        {
            if (!Enum.TryParse<RuleEvaluationType>(entity.Category, true, out var parsedEvalType))
            {
                parsedEvalType = RuleEvaluationType.Completeness;
            }

            RuleSourceMetadata sourceMetadata;
            if (entity.Source != null)
            {
                RuleSourceType sourceType = RuleSourceType.GovernmentOperationalManual;
                if (Enum.TryParse<RuleSourceType>(entity.Source.SourceType, true, out var parsedSourceType))
                {
                    sourceType = parsedSourceType;
                }

                sourceMetadata = new RuleSourceMetadata(
                    sourceType,
                    entity.Source.Authority ?? "Department of National Planning",
                    entity.Source.DocumentTitle ?? "Operational Manual / Project Submission Format",
                    entity.SourceSection ?? "General Operational Guidance",
                    entity.Source.DocumentReference,
                    entity.Source.Checksum,
                    entity.Source.PublishedDate,
                    entity.Source.EffectiveFrom
                );
            }
            else
            {
                sourceMetadata = new RuleSourceMetadata(
                    RuleSourceType.GovernmentOperationalManual,
                    "Department of National Planning",
                    "Operational Manual / Project Submission Format",
                    entity.SourceSection ?? "General Operational Guidance"
                );
            }

            var parametersDict = entity.Parameters != null
                ? entity.Parameters.ToDictionary(p => p.ParameterName, p => p.ParameterValue, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>();

            definitions.Add(new ComplianceRuleDefinition(
                entity.RuleCode,
                entity.RuleVersion,
                entity.Category ?? "General Guidance",
                parsedEvalType,
                entity.Description ?? string.Empty,
                entity.CalculationKey,
                entity.Severity ?? "Medium",
                entity.IsBlocking,
                entity.Enabled,
                sourceMetadata,
                parametersDict
            ));
        }

        return definitions;
    }
}
