namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class DocumentCompletenessAssessment
{
    public DocumentCompletenessAssessmentId Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public string RequirementSetIdentifier { get; }
    public string RequirementSetVersion { get; }
    public DateTime AssessedAt { get; }
    public CompletenessAssessmentOutcome Outcome { get; private set; }
    public int Revision { get; private set; }

    private readonly List<AssessedDocumentBinding> _assessedDocuments;
    public IReadOnlyCollection<AssessedDocumentBinding> AssessedDocuments => _assessedDocuments.AsReadOnly();

    private readonly List<ClassifiedDocument> _classifiedDocuments;
    public IReadOnlyCollection<ClassifiedDocument> ClassifiedDocuments => _classifiedDocuments.AsReadOnly();

    private readonly List<DocumentRequirementSnapshot> _requirements;
    public IReadOnlyCollection<DocumentRequirementSnapshot> Requirements => _requirements.AsReadOnly();

    private readonly List<ClassificationReview> _reviews = new();
    public IReadOnlyCollection<ClassificationReview> Reviews => _reviews.AsReadOnly();

    private List<MissingRequiredDocument> _missingRequirements = new();
    public IReadOnlyCollection<MissingRequiredDocument> MissingRequirements => _missingRequirements.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public DocumentCompletenessAssessment(
        DocumentCompletenessAssessmentId id,
        LeaseCaseId leaseCaseId,
        string requirementSetIdentifier,
        string requirementSetVersion,
        IReadOnlyCollection<AssessedDocumentBinding> assessedDocuments,
        IReadOnlyCollection<ClassifiedDocument> classifiedDocuments,
        IReadOnlyCollection<DocumentRequirementSnapshot> requirements,
        DateTime assessedAt)
    {
        if (id.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("Id cannot be empty.");
        if (leaseCaseId.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("LeaseCaseId cannot be empty.");
        if (string.IsNullOrWhiteSpace(requirementSetIdentifier)) throw new InvalidDocumentCompletenessAssessmentException("RequirementSetIdentifier cannot be empty.");
        if (string.IsNullOrWhiteSpace(requirementSetVersion)) throw new InvalidDocumentCompletenessAssessmentException("RequirementSetVersion cannot be empty.");
        if (assessedAt.Kind != DateTimeKind.Utc) throw new InvalidDocumentCompletenessAssessmentException("AssessedAt must be UTC.");

        if (assessedDocuments == null || !assessedDocuments.Any()) throw new InvalidDocumentCompletenessAssessmentException("At least one assessed document is required.");
        if (classifiedDocuments == null) throw new InvalidDocumentCompletenessAssessmentException("Classified documents collection cannot be null.");
        if (requirements == null) throw new InvalidDocumentCompletenessAssessmentException("Requirements collection cannot be null.");

        var distinctBindings = new HashSet<AssessedDocumentBinding>();
        foreach (var doc in assessedDocuments)
        {
            if (!distinctBindings.Add(doc))
            {
                throw new InvalidDocumentCompletenessAssessmentException("Duplicate AssessedDocumentBinding detected.");
            }
        }

        var distinctClassifications = new HashSet<ClassifiedDocumentId>();
        var boundClassifications = new HashSet<AssessedDocumentBinding>();
        foreach (var classification in classifiedDocuments)
        {
            if (!distinctClassifications.Add(classification.Id))
            {
                throw new InvalidDocumentCompletenessAssessmentException("Duplicate ClassifiedDocumentId detected.");
            }
            if (!distinctBindings.Contains(classification.DocumentBinding))
            {
                throw new InvalidDocumentCompletenessAssessmentException("Classified document refers to an unassessed binding.");
            }
            if (!boundClassifications.Add(classification.DocumentBinding))
            {
                throw new InvalidDocumentCompletenessAssessmentException("Multiple classifications provided for the same document binding.");
            }
        }

        if (boundClassifications.Count != distinctBindings.Count)
        {
            throw new InvalidDocumentCompletenessAssessmentException("Every AssessedDocumentBinding must have exactly one ClassifiedDocument.");
        }

        var reqIds = new HashSet<DocumentRequirementId>();
        var reqCodes = new HashSet<DocumentClassificationCode>();
        foreach (var req in requirements)
        {
            if (!reqIds.Add(req.Id)) throw new InvalidDocumentCompletenessAssessmentException("Duplicate DocumentRequirementId detected.");
            if (!reqCodes.Add(req.RequiredClassificationCode)) throw new InvalidDocumentCompletenessAssessmentException("Duplicate requirement classification code detected.");
        }

        Id = id;
        LeaseCaseId = leaseCaseId;
        RequirementSetIdentifier = requirementSetIdentifier;
        RequirementSetVersion = requirementSetVersion;
        AssessedAt = assessedAt;
        Revision = 1;

        _assessedDocuments = assessedDocuments
            .OrderBy(d => d.GovernedDocumentId.Value)
            .ThenBy(d => d.DocumentVersionId.Value)
            .ToList();

        _classifiedDocuments = classifiedDocuments
            .OrderBy(c => c.DocumentBinding.GovernedDocumentId.Value)
            .ThenBy(c => c.DocumentBinding.DocumentVersionId.Value)
            .ThenBy(c => c.Id.Value)
            .ToList();

        _requirements = requirements
            .OrderBy(r => r.RequiredClassificationCode.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id.Value)
            .ToList();

        var (outcome, missing) = CalculateOutcome(_classifiedDocuments, _reviews, _requirements);
        Outcome = outcome;
        _missingRequirements = missing;

        int applicableReqs = _requirements.Count(r => r.Applicability == RequirementApplicability.Required);
        int humanCandidates = _classifiedDocuments.Count(c => c.Status == DocumentClassificationStatus.Uncertain || c.Status == DocumentClassificationStatus.Unclassified);

        _domainEvents.Add(new DocumentCompletenessAssessed(
            Guid.NewGuid(),
            assessedAt,
            Id,
            LeaseCaseId,
            RequirementSetIdentifier,
            RequirementSetVersion,
            Outcome,
            _assessedDocuments.Count,
            applicableReqs,
            _missingRequirements.Count,
            humanCandidates,
            Revision
        ));
    }

    private static DocumentClassificationCode? GetEffectiveCode(ClassifiedDocument doc, ClassificationReview? review)
    {
        if (review != null)
        {
            if (review.Decision == ClassificationReviewDecision.Confirmed) return doc.OriginalClassificationCode;
            if (review.Decision == ClassificationReviewDecision.Corrected) return review.CorrectedClassificationCode;
            return null; // Unsupported
        }

        if (doc.Status == DocumentClassificationStatus.Accepted) return doc.OriginalClassificationCode;
        
        return null;
    }

    private static (CompletenessAssessmentOutcome Outcome, List<MissingRequiredDocument> Missing) CalculateOutcome(
        IReadOnlyCollection<ClassifiedDocument> documents,
        IReadOnlyCollection<ClassificationReview> reviews,
        IReadOnlyCollection<DocumentRequirementSnapshot> requirements)
    {
        if (requirements.Any(r => r.Applicability == RequirementApplicability.Undetermined))
        {
            return (CompletenessAssessmentOutcome.InsufficientInformation, new List<MissingRequiredDocument>());
        }

        var effectiveCodes = new List<DocumentClassificationCode>();
        var potentialCodes = new List<DocumentClassificationCode>();

        foreach (var doc in documents)
        {
            var review = reviews.FirstOrDefault(r => r.ClassifiedDocumentId.Value == doc.Id.Value);
            var effectiveCode = GetEffectiveCode(doc, review);
            if (effectiveCode != null)
            {
                effectiveCodes.Add(effectiveCode);
            }
            else if (review == null)
            {
                // Can potentially become a match
                if (doc.Status == DocumentClassificationStatus.Uncertain && doc.OriginalClassificationCode != null)
                {
                    potentialCodes.Add(doc.OriginalClassificationCode);
                }
                else if (doc.Status == DocumentClassificationStatus.Unclassified)
                {
                    // A human could classify this as anything, so it represents a wildcard potential match
                    // We don't add a specific code, but we track the existence of wildcards.
                }
            }
        }

        int unclassifiedUnreviewedCount = documents.Count(d => 
            d.Status == DocumentClassificationStatus.Unclassified && 
            !reviews.Any(r => r.ClassifiedDocumentId.Value == d.Id.Value));

        var missingList = new List<MissingRequiredDocument>();
        bool requiresHumanReview = false;

        foreach (var req in requirements)
        {
            if (req.Applicability != RequirementApplicability.Required) continue;
            if (req.Criticality == RequirementCriticality.Optional) continue;

            int satisfied = effectiveCodes.Count(c => c.Equals(req.RequiredClassificationCode));

            if (satisfied < req.MinimumRequiredCount)
            {
                int missingCount = req.MinimumRequiredCount - satisfied;
                missingList.Add(new MissingRequiredDocument(
                    req.Id,
                    req.RequiredClassificationCode,
                    req.MinimumRequiredCount,
                    satisfied,
                    req.Description
                ));

                int potentialMatchCount = potentialCodes.Count(c => c.Equals(req.RequiredClassificationCode));
                if (potentialMatchCount > 0 || unclassifiedUnreviewedCount > 0)
                {
                    requiresHumanReview = true;
                    // Consume wildcards conceptually? We don't actually deduct wildcards because 
                    // multiple requirements could potentially be fulfilled if we had enough wildcards.
                    // If we have at least one unmet requirement that COULD be met, requiresHumanReview = true.
                }
            }
        }

        if (missingList.Count == 0) return (CompletenessAssessmentOutcome.Complete, missingList);
        
        // If there's any missing requirement that CANNOT possibly be met by human review
        // (i.e. no wildcards and no potential matches for that specific requirement)
        // then the outcome should be MissingRequiredDocuments.
        // Wait, the rule is: "MissingRequiredDocuments: No unresolved potential classification could satisfy the deficit, but an applicable mandatory or conditional requirement remains unmet."
        // Does "No unresolved potential" mean for THAT requirement, or ANY requirement?
        // "RequiresHumanReview: Applicability is resolved, but at least one unmet applicable requirement could potentially be satisfied by an unreviewed Uncertain classification with a matching candidate code, or an unreviewed Unclassified document."
        // So if we have at least one missing requirement that has NO potential matches (and no wildcards), is the outcome MissingRequiredDocuments even if another missing requirement DOES have potential matches?
        // Yes, because "MissingRequiredDocuments" has lower precedence than "RequiresHumanReview" or wait:
        // Precedence:
        // 1. InsufficientInformation
        // 2. RequiresHumanReview
        // 3. MissingRequiredDocuments
        // 4. Complete
        // Precedence 2 comes before 3. So if ANY unmet requirement COULD be satisfied, it's RequiresHumanReview?
        // The rule: "b. RequiresHumanReview: Applicability is resolved, but at least one applicable requirement can only be matched by an uncertain or unsupported/unreviewed classification."
        // Let's follow precedence 2: "at least one applicable requirement can only be matched by an uncertain or unreviewed classification."
        if (requiresHumanReview)
        {
            return (CompletenessAssessmentOutcome.RequiresHumanReview, missingList);
        }

        return (CompletenessAssessmentOutcome.MissingRequiredDocuments, missingList);
    }

    public void RecordHumanClassificationReview(
        ClassificationReviewId reviewId,
        ClassifiedDocumentId classifiedDocumentId,
        ClassificationReviewDecision decision,
        DocumentClassificationCode? correctedClassificationCode,
        string? reason,
        Guid reviewingActorId,
        DateTime reviewedAt,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (reviewId.Value == Guid.Empty) throw new InvalidClassificationReviewException("ReviewId cannot be empty.");
        if (classifiedDocumentId.Value == Guid.Empty) throw new InvalidClassificationReviewException("ClassifiedDocumentId cannot be empty.");
        if (reviewingActorId == Guid.Empty) throw new InvalidClassificationReviewException("ReviewingActorId cannot be empty.");
        if (authoritySnapshot == null) throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        if (reviewedAt.Kind != DateTimeKind.Utc) throw new InvalidClassificationReviewException("ReviewedAt must be UTC.");

        if (reviewedAt < AssessedAt) throw new InvalidClassificationReviewException("ReviewedAt cannot be before AssessedAt.");
        if (reviewedAt < authoritySnapshot.VerificationTime) throw new InvalidClassificationReviewException("ReviewedAt cannot be before authority verification time.");

        var doc = _classifiedDocuments.FirstOrDefault(c => c.Id.Value == classifiedDocumentId.Value);
        if (doc == null) throw new ClassifiedDocumentNotFoundException("ClassifiedDocument not found.");

        if (_reviews.Any(r => r.Id.Value == reviewId.Value))
        {
            var existing = _reviews.First(r => r.Id.Value == reviewId.Value);
            bool isSame = existing.ClassifiedDocumentId.Value == classifiedDocumentId.Value &&
                          existing.Decision == decision &&
                          (existing.CorrectedClassificationCode?.Equals(correctedClassificationCode) ?? correctedClassificationCode == null) &&
                          string.Equals(existing.Reason, reason, StringComparison.Ordinal) &&
                          existing.ReviewingActorId == reviewingActorId &&
                          existing.ReviewedAt == reviewedAt;

            if (isSame) throw new DuplicateClassificationReviewException("Identical review already exists.");
            throw new ConflictingClassificationReviewException("Conflicting review ID already exists.");
        }

        if (_reviews.Any(r => r.ClassifiedDocumentId.Value == classifiedDocumentId.Value))
        {
            throw new ClassificationAlreadyReviewedException("ClassifiedDocument is already reviewed.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, doc.DocumentBinding.GovernedDocumentId.Value.ToString("D"));
        authoritySnapshot.EnsureAuthorizes(reviewingActorId, "ClassificationReviewer", requiredScope, reviewedAt);

        var candidate = new ClassificationReview(
            reviewId,
            classifiedDocumentId,
            doc.OriginalClassificationCode,
            decision,
            correctedClassificationCode,
            reason,
            reviewingActorId,
            reviewedAt,
            "ClassificationReviewer",
            authoritySnapshot.Scope.Kind,
            authoritySnapshot.Scope.TargetIdentifier,
            requiredScope.Kind,
            requiredScope.TargetIdentifier!,
            authoritySnapshot.VerificationTime
        );

        int nextRevision;
        try
        {
            nextRevision = checked(Revision + 1);
        }
        catch (OverflowException)
        {
            throw new DocumentCompletenessRevisionOverflowException("Revision overflow.");
        }

        var futureReviews = _reviews.ToList();
        futureReviews.Add(candidate);

        var (newOutcome, newMissing) = CalculateOutcome(_classifiedDocuments, futureReviews, _requirements);

        var effectiveCode = GetEffectiveCode(doc, candidate);

        var evt = new DocumentClassificationHumanReviewed(
            Guid.NewGuid(),
            reviewedAt,
            Id,
            LeaseCaseId,
            reviewId,
            classifiedDocumentId,
            doc.DocumentBinding.GovernedDocumentId,
            doc.DocumentBinding.DocumentVersionId,
            decision,
            doc.OriginalClassificationCode?.Value,
            correctedClassificationCode?.Value,
            effectiveCode?.Value,
            reviewingActorId,
            nextRevision
        );

        _reviews.Add(candidate);
        Outcome = newOutcome;
        _missingRequirements = newMissing;
        Revision = nextRevision;
        _domainEvents.Add(evt);
    }
}
