namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ProposalTemplate
{
    public ProposalTemplateId Id { get; }
    public string Version { get; }
    public string Name { get; }
    public string AuthoritativeSourceReference { get; }
    public ProposalTemplateEffectivePeriod? EffectivePeriod { get; }
    public ProposalTemplateStatus Status { get; }

    private readonly List<ProposalContentRequirement> _requirements;
    public IReadOnlyCollection<ProposalContentRequirement> Requirements => _requirements.AsReadOnly();

    public ProposalTemplate(
        ProposalTemplateId id,
        string version,
        string name,
        string authoritativeSourceReference,
        ProposalTemplateStatus status,
        IEnumerable<ProposalContentRequirement> requirements,
        ProposalTemplateEffectivePeriod? effectivePeriod = null)
    {
        if (id == default || string.IsNullOrWhiteSpace(id.Value))
        {
            throw new InvalidProposalTemplateException("TemplateId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidProposalTemplateException("Version cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidProposalTemplateException("Name cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(authoritativeSourceReference))
        {
            throw new InvalidProposalTemplateException("AuthoritativeSourceReference cannot be null, empty, or whitespace.");
        }

        if (!Enum.IsDefined(typeof(ProposalTemplateStatus), status))
        {
            throw new InvalidProposalTemplateException($"Invalid ProposalTemplateStatus: {status}.");
        }

        if (requirements == null)
        {
            throw new InvalidProposalTemplateException("Requirements collection cannot be null.");
        }

        var reqList = requirements.ToList();
        if (reqList.Count == 0)
        {
            throw new InvalidProposalTemplateException("Template must contain at least one requirement.");
        }

        var seenIds = new HashSet<ProposalRequirementId>();
        var seenOrderIndices = new HashSet<int>();

        foreach (var req in reqList)
        {
            if (req == null)
            {
                throw new InvalidProposalTemplateException("Requirement in collection cannot be null.");
            }

            if (!req.TemplateId.Equals(id))
            {
                throw new InvalidProposalTemplateException($"Requirement '{req.Id}' belongs to template '{req.TemplateId}', expected '{id}'.");
            }

            if (!string.Equals(req.TemplateVersion, version.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidProposalTemplateException($"Requirement '{req.Id}' belongs to version '{req.TemplateVersion}', expected '{version.Trim()}'.");
            }

            if (!seenIds.Add(req.Id))
            {
                throw new InvalidProposalTemplateException($"Duplicate requirement identifier '{req.Id}' within template version.");
            }

            if (!seenOrderIndices.Add(req.OrderIndex))
            {
                throw new InvalidProposalTemplateException($"Duplicate order index '{req.OrderIndex}' detected in template requirements.");
            }
        }

        Id = id;
        Version = version.Trim();
        Name = name.Trim();
        AuthoritativeSourceReference = authoritativeSourceReference.Trim();
        Status = status;
        EffectivePeriod = effectivePeriod;
        _requirements = reqList.OrderBy(r => r.OrderIndex).ToList();
    }

    public bool IsEffectiveAt(DateTime timestampUtc)
    {
        if (EffectivePeriod == null) return true;
        return EffectivePeriod.IsEffectiveAt(timestampUtc);
    }

    public bool IsActiveAt(DateTime timestampUtc)
    {
        return Status == ProposalTemplateStatus.Active && IsEffectiveAt(timestampUtc);
    }

    public bool ContainsRequirement(ProposalRequirementId id)
    {
        return _requirements.Any(r => r.Id.Equals(id));
    }

    public ProposalContentRequirement? GetRequirement(ProposalRequirementId id)
    {
        return _requirements.FirstOrDefault(r => r.Id.Equals(id));
    }

    public ProposalTemplateSnapshot CreateSnapshot()
    {
        return ProposalTemplateSnapshot.FromTemplate(this);
    }

    public string DefinitionDigest => CreateSnapshot().DefinitionDigest;
}
