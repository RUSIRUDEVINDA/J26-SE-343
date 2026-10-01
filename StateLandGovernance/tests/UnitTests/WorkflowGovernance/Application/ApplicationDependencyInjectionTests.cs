namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;

public class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddWorkflowGovernanceApplication_RegistersHandlersAndValidators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddWorkflowGovernanceApplication();

        // Assert: 4A.1 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.1 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.ImplementationType == typeof(GetLeaseCaseByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.1 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RegisterLeaseCaseCommand>) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.ImplementationType == typeof(RegisterLeaseCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(AddDocumentVersionCommandHandler) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetGovernedDocumentByIdQuery, GovernedDocumentDto>) &&
            d.ImplementationType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.ImplementationType == typeof(GetGovernedDocumentByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>>) &&
            d.ImplementationType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.ImplementationType == typeof(GetDocumentsByLeaseCaseIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.2 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RegisterGovernedDocumentCommand>) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.ImplementationType == typeof(RegisterGovernedDocumentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<AddDocumentVersionCommand>) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(AddDocumentVersionCommandValidator) &&
            d.ImplementationType == typeof(AddDocumentVersionCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.3 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<RequestDocumentAnalysisCommand, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(RequestDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RequestDocumentAnalysisCommandHandler) &&
            d.ImplementationType == typeof(RequestDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<StartDocumentAnalysisRunCommand, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(StartDocumentAnalysisRunCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(StartDocumentAnalysisRunCommandHandler) &&
            d.ImplementationType == typeof(StartDocumentAnalysisRunCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<CompleteDocumentAnalysisCommand, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(CompleteDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(CompleteDocumentAnalysisCommandHandler) &&
            d.ImplementationType == typeof(CompleteDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<FailDocumentAnalysisCommand, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(FailDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(FailDocumentAnalysisCommandHandler) &&
            d.ImplementationType == typeof(FailDocumentAnalysisCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.3 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetDocumentAnalysisByIdQuery, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(GetDocumentAnalysisByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetDocumentAnalysisByIdQueryHandler) &&
            d.ImplementationType == typeof(GetDocumentAnalysisByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetDocumentAnalysisByVersionIdQuery, DocumentAnalysisDto>) &&
            d.ImplementationType == typeof(GetDocumentAnalysisByVersionIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(GetDocumentAnalysisByVersionIdQueryHandler) &&
            d.ImplementationType == typeof(GetDocumentAnalysisByVersionIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.3 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<RequestDocumentAnalysisCommand>) &&
            d.ImplementationType == typeof(RequestDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(RequestDocumentAnalysisCommandValidator) &&
            d.ImplementationType == typeof(RequestDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<StartDocumentAnalysisRunCommand>) &&
            d.ImplementationType == typeof(StartDocumentAnalysisRunCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(StartDocumentAnalysisRunCommandValidator) &&
            d.ImplementationType == typeof(StartDocumentAnalysisRunCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<CompleteDocumentAnalysisCommand>) &&
            d.ImplementationType == typeof(CompleteDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(CompleteDocumentAnalysisCommandValidator) &&
            d.ImplementationType == typeof(CompleteDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<FailDocumentAnalysisCommand>) &&
            d.ImplementationType == typeof(FailDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(FailDocumentAnalysisCommandValidator) &&
            d.ImplementationType == typeof(FailDocumentAnalysisCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.4 Command Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<VerifyCandidateFactCommand, HumanFactVerificationDto>) &&
            d.ImplementationType == typeof(VerifyCandidateFactCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<CreateVerifiedFactSnapshotCommand, VerifiedFactSnapshotDto>) &&
            d.ImplementationType == typeof(CreateVerifiedFactSnapshotCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<LinkVerifiedFactSnapshotToCaseCommand, LeaseCaseDto>) &&
            d.ImplementationType == typeof(LinkVerifiedFactSnapshotToCaseCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<AssessDocumentCompletenessCommand, DocumentCompletenessAssessmentDto>) &&
            d.ImplementationType == typeof(AssessDocumentCompletenessCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<ReviewDocumentClassificationCommand, DocumentCompletenessAssessmentDto>) &&
            d.ImplementationType == typeof(ReviewDocumentClassificationCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto>) &&
            d.ImplementationType == typeof(AssessProposalContentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(AssessProposalContentCommandHandler) &&
            d.ImplementationType == typeof(AssessProposalContentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<ConfirmProposalContentAssessmentCommand, ProposalContentAssessmentDto>) &&
            d.ImplementationType == typeof(ConfirmProposalContentAssessmentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ICommandHandler<CorrectProposalContentAssessmentCommand, ProposalContentAssessmentDto>) &&
            d.ImplementationType == typeof(CorrectProposalContentAssessmentCommandHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.4 Query Handlers
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetVerifiedFactSnapshotByIdQuery, VerifiedFactSnapshotDto?>) &&
            d.ImplementationType == typeof(GetVerifiedFactSnapshotByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetCurrentVerifiedFactsForCaseQuery, VerifiedFactSnapshotDto?>) &&
            d.ImplementationType == typeof(GetCurrentVerifiedFactsForCaseQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetDocumentCompletenessAssessmentByIdQuery, DocumentCompletenessAssessmentDto?>) &&
            d.ImplementationType == typeof(GetDocumentCompletenessAssessmentByIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetLatestDocumentCompletenessAssessmentByCaseIdQuery, DocumentCompletenessAssessmentDto?>) &&
            d.ImplementationType == typeof(GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetProposalContentAssessmentQuery, ProposalContentAssessmentDto?>) &&
            d.ImplementationType == typeof(GetProposalContentAssessmentQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IQueryHandler<GetCurrentProposalContentAssessmentQuery, ProposalContentAssessmentDto?>) &&
            d.ImplementationType == typeof(GetCurrentProposalContentAssessmentQueryHandler) &&
            d.Lifetime == ServiceLifetime.Scoped);

        // Assert: 4A.4 Validators
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<VerifyCandidateFactCommand>) &&
            d.ImplementationType == typeof(VerifyCandidateFactCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<CreateVerifiedFactSnapshotCommand>) &&
            d.ImplementationType == typeof(CreateVerifiedFactSnapshotCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<LinkVerifiedFactSnapshotToCaseCommand>) &&
            d.ImplementationType == typeof(LinkVerifiedFactSnapshotToCaseCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<AssessDocumentCompletenessCommand>) &&
            d.ImplementationType == typeof(AssessDocumentCompletenessCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<ReviewDocumentClassificationCommand>) &&
            d.ImplementationType == typeof(ReviewDocumentClassificationCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<AssessProposalContentCommand>) &&
            d.ImplementationType == typeof(AssessProposalContentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<ConfirmProposalContentAssessmentCommand>) &&
            d.ImplementationType == typeof(ConfirmProposalContentAssessmentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IRequestValidator<CorrectProposalContentAssessmentCommand>) &&
            d.ImplementationType == typeof(CorrectProposalContentAssessmentCommandValidator) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
