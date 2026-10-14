namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed record ProposalTemplateSnapshot
{
    public ProposalTemplateId TemplateId { get; }
    public string TemplateVersion { get; }
    public string Name { get; }
    public string AuthoritativeSourceReference { get; }
    public ProposalTemplateStatus Status { get; }
    public ProposalTemplateEffectivePeriod? EffectivePeriod { get; }
    public IReadOnlyList<ProposalContentRequirement> Requirements { get; }
    public string DefinitionDigest { get; }

    public ProposalTemplateSnapshot(
        ProposalTemplateId templateId,
        string templateVersion,
        string name,
        string authoritativeSourceReference,
        ProposalTemplateStatus status,
        IEnumerable<ProposalContentRequirement> requirements,
        ProposalTemplateEffectivePeriod? effectivePeriod = null)
    {
        if (templateId == default || string.IsNullOrWhiteSpace(templateId.Value))
        {
            throw new InvalidProposalTemplateException("TemplateId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(templateVersion))
        {
            throw new InvalidProposalTemplateException("TemplateVersion cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidProposalTemplateException("Name cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(authoritativeSourceReference))
        {
            throw new InvalidProposalTemplateException("AuthoritativeSourceReference cannot be null, empty, or whitespace.");
        }

        if (requirements == null)
        {
            throw new InvalidProposalTemplateException("Requirements collection cannot be null.");
        }

        var reqList = requirements
            .OrderBy(r => r.OrderIndex)
            .ThenBy(r => r.Id.Value, StringComparer.Ordinal)
            .ToList();

        if (reqList.Count == 0)
        {
            throw new InvalidProposalTemplateException("Template must contain at least one requirement.");
        }

        TemplateId = templateId;
        TemplateVersion = templateVersion.Trim();
        Name = name.Trim();
        AuthoritativeSourceReference = authoritativeSourceReference.Trim();
        Status = status;
        EffectivePeriod = effectivePeriod;
        Requirements = reqList.AsReadOnly();
        DefinitionDigest = ComputeCanonicalDigest(
            TemplateId,
            TemplateVersion,
            Name,
            AuthoritativeSourceReference,
            Status,
            EffectivePeriod,
            reqList
        );
    }

    public static ProposalTemplateSnapshot FromTemplate(ProposalTemplate template)
    {
        if (template == null)
        {
            throw new InvalidProposalTemplateException("ProposalTemplate cannot be null.");
        }

        return new ProposalTemplateSnapshot(
            template.Id,
            template.Version,
            template.Name,
            template.AuthoritativeSourceReference,
            template.Status,
            template.Requirements,
            template.EffectivePeriod
        );
    }

    private static string ComputeCanonicalDigest(
        ProposalTemplateId id,
        string version,
        string name,
        string sourceRef,
        ProposalTemplateStatus status,
        ProposalTemplateEffectivePeriod? effectivePeriod,
        IReadOnlyList<ProposalContentRequirement> reqs)
    {
        var sb = new StringBuilder();

        // 1. Template-level properties
        WriteField(sb, id.Value);
        WriteField(sb, version);
        WriteField(sb, name);
        WriteField(sb, sourceRef);
        WriteField(sb, status.ToString());
        WriteField(sb, effectivePeriod?.ValidFromUtc?.ToString("O"));
        WriteField(sb, effectivePeriod?.ValidToUtc?.ToString("O"));

        // 2. Count of requirements
        WriteField(sb, reqs.Count.ToString());

        // 3. Deterministically sorted requirements
        foreach (var r in reqs)
        {
            WriteField(sb, r.Id.Value);
            WriteField(sb, ((int)r.Kind).ToString());
            WriteField(sb, r.IsMandatory ? "1" : "0");
            WriteField(sb, r.OrderIndex.ToString());
            WriteField(sb, r.DisplayName);
            WriteField(sb, r.AuthoritativeSourceReference);
            WriteField(sb, r.ApplicabilityContext);
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static void WriteField(StringBuilder sb, string? value)
    {
        if (value is null)
        {
            sb.Append("-1:\n");
        }
        else
        {
            var byteCount = Encoding.UTF8.GetByteCount(value);
            sb.Append(byteCount).Append(':').Append(value).Append('\n');
        }
    }
}
