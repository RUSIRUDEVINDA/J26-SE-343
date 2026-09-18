namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public static class ProposalContentCompletenessEvaluator
{
    public static ProposalContentCompletenessResult Evaluate(
        ProposalSourceBinding sourceBinding,
        ProposalTemplate? template,
        IEnumerable<ProposalRequirementObservation>? observations)
    {
        if (sourceBinding == null)
        {
            throw new InvalidProposalSourceBindingException("ProposalSourceBinding cannot be null.");
        }

        var resultId = ProposalContentAssessmentResultId.New();

        // 1. If template is missing -> Undetermined
        if (template == null)
        {
            var placeholderReq = new ProposalContentRequirement(
                new ProposalRequirementId("UNAVAILABLE_TEMPLATE_REQUIREMENT"),
                ProposalRequirementKind.Section,
                "Unavailable Template Requirement",
                isMandatory: true,
                sourceBinding.TemplateId,
                sourceBinding.TemplateVersion,
                "UNAVAILABLE",
                1
            );

            var placeholderSnapshot = new ProposalTemplateSnapshot(
                sourceBinding.TemplateId,
                sourceBinding.TemplateVersion,
                "Missing Template",
                "UNAVAILABLE",
                ProposalTemplateStatus.Inactive,
                new[] { placeholderReq }
            );

            return new ProposalContentCompletenessResult(
                resultId,
                sourceBinding.LeaseCaseId,
                sourceBinding,
                placeholderSnapshot,
                ProposalContentCompletenessOutcome.Undetermined,
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                new[] { "Proposal template is missing or not configured." },
                isConfirmed: false
            );
        }

        // 2. Validate template match against source binding
        if (!sourceBinding.MatchesTemplate(template.Id, template.Version))
        {
            throw new InvalidProposalContentAssessmentException(
                $"SourceBinding template '{sourceBinding.TemplateId}' version '{sourceBinding.TemplateVersion}' " +
                $"does not match template '{template.Id}' version '{template.Version}'.");
        }

        var templateSnapshot = template.CreateSnapshot();

        // 3. If template is Inactive -> Undetermined
        if (template.Status == ProposalTemplateStatus.Inactive)
        {
            return new ProposalContentCompletenessResult(
                resultId,
                sourceBinding.LeaseCaseId,
                sourceBinding,
                templateSnapshot,
                ProposalContentCompletenessOutcome.Undetermined,
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                new[] { $"Proposal template '{template.Id}' version '{template.Version}' is Inactive." },
                isConfirmed: false
            );
        }

        // 4. If template is out of period -> Undetermined
        if (!template.IsEffectiveAt(sourceBinding.AssessedAt))
        {
            return new ProposalContentCompletenessResult(
                resultId,
                sourceBinding.LeaseCaseId,
                sourceBinding,
                templateSnapshot,
                ProposalContentCompletenessOutcome.Undetermined,
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                Array.Empty<ProposalContentRequirement>(),
                new[] { $"Proposal template '{template.Id}' version '{template.Version}' is not effective at assessment time {sourceBinding.AssessedAt:O}." },
                isConfirmed: false
            );
        }

        // 5. Process and validate observations
        var obsList = observations?.ToList() ?? new List<ProposalRequirementObservation>();
        var obsMap = new Dictionary<ProposalRequirementId, ProposalRequirementObservation>();

        foreach (var obs in obsList)
        {
            if (obs == null)
            {
                throw new InvalidProposalObservationException("Observation in collection cannot be null.");
            }

            // Unknown observation rejection
            if (!template.ContainsRequirement(obs.RequirementId))
            {
                throw new InvalidProposalObservationException(
                    $"Observation references unknown requirement '{obs.RequirementId}' not belonging to template '{template.Id}' version '{template.Version}'.");
            }

            // Duplicate observation rejection
            if (!obsMap.TryAdd(obs.RequirementId, obs))
            {
                throw new InvalidProposalObservationException(
                    $"Duplicate observation detected for requirement '{obs.RequirementId}'.");
            }

            // Exact equality between observation binding and assessment binding
            if (obs.SourceBinding == null)
            {
                throw new InvalidProposalObservationException("Observation SourceBinding cannot be null.");
            }

            if (!obs.SourceBinding.MatchesSource(sourceBinding))
            {
                if (!obs.SourceBinding.LeaseCaseId.Equals(sourceBinding.LeaseCaseId))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references LeaseCaseId '{obs.SourceBinding.LeaseCaseId}', " +
                        $"expected '{sourceBinding.LeaseCaseId}'.");
                }

                if (!obs.SourceBinding.ProposalDocumentId.Equals(sourceBinding.ProposalDocumentId))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references GovernedDocumentId '{obs.SourceBinding.ProposalDocumentId}', " +
                        $"expected '{sourceBinding.ProposalDocumentId}'.");
                }

                if (!obs.SourceBinding.DocumentVersionId.Equals(sourceBinding.DocumentVersionId))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references document version '{obs.SourceBinding.DocumentVersionId}', " +
                        $"expected '{sourceBinding.DocumentVersionId}'.");
                }

                if (!obs.SourceBinding.DocumentChecksum.Equals(sourceBinding.DocumentChecksum))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references checksum '{obs.SourceBinding.DocumentChecksum.Value}', " +
                        $"expected '{sourceBinding.DocumentChecksum.Value}'.");
                }

                if (!obs.SourceBinding.TemplateId.Equals(sourceBinding.TemplateId))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references template '{obs.SourceBinding.TemplateId}', " +
                        $"expected '{sourceBinding.TemplateId}'.");
                }

                if (!string.Equals(obs.SourceBinding.TemplateVersion, sourceBinding.TemplateVersion, StringComparison.Ordinal))
                {
                    throw new InvalidProposalObservationException(
                        $"Observation for requirement '{obs.RequirementId}' references template version '{obs.SourceBinding.TemplateVersion}', " +
                        $"expected '{sourceBinding.TemplateVersion}'.");
                }

                throw new InvalidProposalObservationException(
                    $"Observation for requirement '{obs.RequirementId}' binding does not match assessment source binding.");
            }
        }

        // 6. Categorize requirements
        var satisfiedMandatory = new List<ProposalContentRequirement>();
        var missingMandatory = new List<ProposalContentRequirement>();
        var reviewRequiredMandatory = new List<ProposalContentRequirement>();
        var missingOptional = new List<ProposalContentRequirement>();
        var satisfiedOptional = new List<ProposalContentRequirement>();
        var unresolvedOptional = new List<ProposalContentRequirement>();
        var explanations = new List<string>();

        foreach (var req in template.Requirements.OrderBy(r => r.OrderIndex))
        {
            if (obsMap.TryGetValue(req.Id, out var obs))
            {
                switch (obs.State)
                {
                    case ProposalObservationState.Present:
                        if (req.IsMandatory)
                        {
                            satisfiedMandatory.Add(req);
                        }
                        else
                        {
                            satisfiedOptional.Add(req);
                        }
                        break;

                    case ProposalObservationState.Missing:
                        if (req.IsMandatory)
                        {
                            missingMandatory.Add(req);
                            explanations.Add($"Mandatory {req.Kind.ToString().ToLowerInvariant()} '{req.DisplayName}' ({req.Id}) is confirmed missing: {obs.Explanation ?? obs.EvidenceReference}.");
                        }
                        else
                        {
                            missingOptional.Add(req);
                        }
                        break;

                    case ProposalObservationState.Uncertain:
                    case ProposalObservationState.Unreadable:
                    case ProposalObservationState.Unsupported:
                        if (req.IsMandatory)
                        {
                            reviewRequiredMandatory.Add(req);
                            explanations.Add($"Mandatory {req.Kind.ToString().ToLowerInvariant()} '{req.DisplayName}' ({req.Id}) is {obs.State.ToString().ToLowerInvariant()} and requires human review: {obs.Explanation ?? obs.EvidenceReference}.");
                        }
                        else
                        {
                            unresolvedOptional.Add(req);
                            explanations.Add($"Optional {req.Kind.ToString().ToLowerInvariant()} '{req.DisplayName}' ({req.Id}) is {obs.State.ToString().ToLowerInvariant()}: {obs.Explanation ?? obs.EvidenceReference}.");
                        }
                        break;
                }
            }
            else
            {
                // No observation provided
                if (req.IsMandatory)
                {
                    // Absence of observation for mandatory requirement requires human review
                    reviewRequiredMandatory.Add(req);
                    explanations.Add($"Mandatory {req.Kind.ToString().ToLowerInvariant()} '{req.DisplayName}' ({req.Id}) has no extraction observation and requires human review.");
                }
                else
                {
                    // No observation for optional requirement does not require human review or block completeness
                    missingOptional.Add(req);
                }
            }
        }

        // 7. Determine outcome following strict precedence:
        // Precedence:
        // 1. Template Draft or AwaitingConfirmation -> HumanReviewRequired
        // 2. Any mandatory Uncertain, Unreadable, Unsupported, or absent observation -> HumanReviewRequired
        // 3. Otherwise, any explicitly Missing mandatory requirement -> Incomplete
        // 4. Otherwise, all mandatory requirements present -> Complete
        ProposalContentCompletenessOutcome outcome;

        if (template.Status == ProposalTemplateStatus.Draft || template.Status == ProposalTemplateStatus.AwaitingConfirmation)
        {
            outcome = ProposalContentCompletenessOutcome.HumanReviewRequired;
            explanations.Insert(0, $"Template '{template.Id}' is in {template.Status} status and requires human confirmation before official use.");
        }
        else if (reviewRequiredMandatory.Count > 0)
        {
            outcome = ProposalContentCompletenessOutcome.HumanReviewRequired;
            explanations.Insert(0, $"{reviewRequiredMandatory.Count} mandatory requirement(s) require human review due to uncertain, unreadable, unsupported, or absent extraction observations.");
        }
        else if (missingMandatory.Count > 0)
        {
            outcome = ProposalContentCompletenessOutcome.Incomplete;
            explanations.Insert(0, $"{missingMandatory.Count} mandatory requirement(s) are confirmed missing from the proposal document.");
        }
        else
        {
            outcome = ProposalContentCompletenessOutcome.Complete;
            explanations.Insert(0, "All mandatory proposal sections and fields are confirmed present.");
        }

        return new ProposalContentCompletenessResult(
            resultId,
            sourceBinding.LeaseCaseId,
            sourceBinding,
            templateSnapshot,
            outcome,
            satisfiedMandatory,
            missingMandatory,
            reviewRequiredMandatory,
            missingOptional,
            satisfiedOptional,
            explanations,
            unresolvedOptional,
            isConfirmed: false
        );
    }
}
