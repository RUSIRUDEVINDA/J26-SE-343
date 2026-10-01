namespace StateLandGovernance.WorkflowGovernance.Application;

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowGovernanceApplication(this IServiceCollection services)
    {
        // 4A.1 Handlers & Validators
        services.AddScoped<ICommandHandler<RegisterLeaseCaseCommand, LeaseCaseDto>, RegisterLeaseCaseCommandHandler>();
        services.AddScoped<RegisterLeaseCaseCommandHandler>();

        services.AddScoped<IQueryHandler<GetLeaseCaseByIdQuery, LeaseCaseDto>, GetLeaseCaseByIdQueryHandler>();
        services.AddScoped<GetLeaseCaseByIdQueryHandler>();

        services.AddScoped<IRequestValidator<RegisterLeaseCaseCommand>, RegisterLeaseCaseCommandValidator>();
        services.AddScoped<RegisterLeaseCaseCommandValidator>();

        // 4A.2 Handlers & Validators
        services.AddScoped<ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto>, RegisterGovernedDocumentCommandHandler>();
        services.AddScoped<RegisterGovernedDocumentCommandHandler>();

        services.AddScoped<ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto>, AddDocumentVersionCommandHandler>();
        services.AddScoped<AddDocumentVersionCommandHandler>();

        services.AddScoped<IQueryHandler<GetGovernedDocumentByIdQuery, GovernedDocumentDto>, GetGovernedDocumentByIdQueryHandler>();
        services.AddScoped<GetGovernedDocumentByIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetDocumentsByLeaseCaseIdQuery, IReadOnlyList<GovernedDocumentDto>>, GetDocumentsByLeaseCaseIdQueryHandler>();
        services.AddScoped<GetDocumentsByLeaseCaseIdQueryHandler>();

        services.AddScoped<IRequestValidator<RegisterGovernedDocumentCommand>, RegisterGovernedDocumentCommandValidator>();
        services.AddScoped<RegisterGovernedDocumentCommandValidator>();

        services.AddScoped<IRequestValidator<AddDocumentVersionCommand>, AddDocumentVersionCommandValidator>();
        services.AddScoped<AddDocumentVersionCommandValidator>();

        // 4A.3 Handlers & Validators (Document Analysis Run Orchestration)
        services.AddScoped<ICommandHandler<RequestDocumentAnalysisCommand, DocumentAnalysisDto>, RequestDocumentAnalysisCommandHandler>();
        services.AddScoped<RequestDocumentAnalysisCommandHandler>();

        services.AddScoped<ICommandHandler<StartDocumentAnalysisRunCommand, DocumentAnalysisDto>, StartDocumentAnalysisRunCommandHandler>();
        services.AddScoped<StartDocumentAnalysisRunCommandHandler>();

        services.AddScoped<ICommandHandler<CompleteDocumentAnalysisCommand, DocumentAnalysisDto>, CompleteDocumentAnalysisCommandHandler>();
        services.AddScoped<CompleteDocumentAnalysisCommandHandler>();

        services.AddScoped<ICommandHandler<FailDocumentAnalysisCommand, DocumentAnalysisDto>, FailDocumentAnalysisCommandHandler>();
        services.AddScoped<FailDocumentAnalysisCommandHandler>();

        services.AddScoped<IQueryHandler<GetDocumentAnalysisByIdQuery, DocumentAnalysisDto>, GetDocumentAnalysisByIdQueryHandler>();
        services.AddScoped<GetDocumentAnalysisByIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetDocumentAnalysisByVersionIdQuery, DocumentAnalysisDto>, GetDocumentAnalysisByVersionIdQueryHandler>();
        services.AddScoped<GetDocumentAnalysisByVersionIdQueryHandler>();

        services.AddScoped<IRequestValidator<RequestDocumentAnalysisCommand>, RequestDocumentAnalysisCommandValidator>();
        services.AddScoped<RequestDocumentAnalysisCommandValidator>();

        services.AddScoped<IRequestValidator<StartDocumentAnalysisRunCommand>, StartDocumentAnalysisRunCommandValidator>();
        services.AddScoped<StartDocumentAnalysisRunCommandValidator>();

        services.AddScoped<IRequestValidator<CompleteDocumentAnalysisCommand>, CompleteDocumentAnalysisCommandValidator>();
        services.AddScoped<CompleteDocumentAnalysisCommandValidator>();

        services.AddScoped<IRequestValidator<FailDocumentAnalysisCommand>, FailDocumentAnalysisCommandValidator>();
        services.AddScoped<FailDocumentAnalysisCommandValidator>();

        // 4A.4 Handlers & Validators (Human Fact Verification, Snapshot, Dual Completeness)
        services.AddScoped<ICommandHandler<VerifyCandidateFactCommand, HumanFactVerificationDto>, VerifyCandidateFactCommandHandler>();
        services.AddScoped<VerifyCandidateFactCommandHandler>();

        services.AddScoped<ICommandHandler<CreateVerifiedFactSnapshotCommand, VerifiedFactSnapshotDto>, CreateVerifiedFactSnapshotCommandHandler>();
        services.AddScoped<CreateVerifiedFactSnapshotCommandHandler>();

        services.AddScoped<ICommandHandler<LinkVerifiedFactSnapshotToCaseCommand, LeaseCaseDto>, LinkVerifiedFactSnapshotToCaseCommandHandler>();
        services.AddScoped<LinkVerifiedFactSnapshotToCaseCommandHandler>();

        services.AddScoped<ICommandHandler<AssessDocumentCompletenessCommand, DocumentCompletenessAssessmentDto>, AssessDocumentCompletenessCommandHandler>();
        services.AddScoped<AssessDocumentCompletenessCommandHandler>();

        services.AddScoped<ICommandHandler<ReviewDocumentClassificationCommand, DocumentCompletenessAssessmentDto>, ReviewDocumentClassificationCommandHandler>();
        services.AddScoped<ReviewDocumentClassificationCommandHandler>();

        services.AddScoped<ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto>, AssessProposalContentCommandHandler>();
        services.AddScoped<AssessProposalContentCommandHandler>();

        services.AddScoped<ICommandHandler<ConfirmProposalContentAssessmentCommand, ProposalContentAssessmentDto>, ConfirmProposalContentAssessmentCommandHandler>();
        services.AddScoped<ConfirmProposalContentAssessmentCommandHandler>();

        services.AddScoped<ICommandHandler<CorrectProposalContentAssessmentCommand, ProposalContentAssessmentDto>, CorrectProposalContentAssessmentCommandHandler>();
        services.AddScoped<CorrectProposalContentAssessmentCommandHandler>();

        services.AddScoped<IQueryHandler<GetVerifiedFactSnapshotByIdQuery, VerifiedFactSnapshotDto?>, GetVerifiedFactSnapshotByIdQueryHandler>();
        services.AddScoped<GetVerifiedFactSnapshotByIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetCurrentVerifiedFactsForCaseQuery, VerifiedFactSnapshotDto?>, GetCurrentVerifiedFactsForCaseQueryHandler>();
        services.AddScoped<GetCurrentVerifiedFactsForCaseQueryHandler>();

        services.AddScoped<IQueryHandler<GetDocumentCompletenessAssessmentByIdQuery, DocumentCompletenessAssessmentDto?> , GetDocumentCompletenessAssessmentByIdQueryHandler>();
        services.AddScoped<GetDocumentCompletenessAssessmentByIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetLatestDocumentCompletenessAssessmentByCaseIdQuery, DocumentCompletenessAssessmentDto?>, GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler>();
        services.AddScoped<GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler>();

        services.AddScoped<IQueryHandler<GetProposalContentAssessmentQuery, ProposalContentAssessmentDto?>, GetProposalContentAssessmentQueryHandler>();
        services.AddScoped<GetProposalContentAssessmentQueryHandler>();

        services.AddScoped<IQueryHandler<GetCurrentProposalContentAssessmentQuery, ProposalContentAssessmentDto?>, GetCurrentProposalContentAssessmentQueryHandler>();
        services.AddScoped<GetCurrentProposalContentAssessmentQueryHandler>();

        services.AddScoped<IRequestValidator<VerifyCandidateFactCommand>, VerifyCandidateFactCommandValidator>();
        services.AddScoped<VerifyCandidateFactCommandValidator>();

        services.AddScoped<IRequestValidator<CreateVerifiedFactSnapshotCommand>, CreateVerifiedFactSnapshotCommandValidator>();
        services.AddScoped<CreateVerifiedFactSnapshotCommandValidator>();

        services.AddScoped<IRequestValidator<LinkVerifiedFactSnapshotToCaseCommand>, LinkVerifiedFactSnapshotToCaseCommandValidator>();
        services.AddScoped<LinkVerifiedFactSnapshotToCaseCommandValidator>();

        services.AddScoped<IRequestValidator<AssessDocumentCompletenessCommand>, AssessDocumentCompletenessCommandValidator>();
        services.AddScoped<AssessDocumentCompletenessCommandValidator>();

        services.AddScoped<IRequestValidator<ReviewDocumentClassificationCommand>, ReviewDocumentClassificationCommandValidator>();
        services.AddScoped<ReviewDocumentClassificationCommandValidator>();

        services.AddScoped<IRequestValidator<AssessProposalContentCommand>, AssessProposalContentCommandValidator>();
        services.AddScoped<AssessProposalContentCommandValidator>();

        services.AddScoped<IRequestValidator<ConfirmProposalContentAssessmentCommand>, ConfirmProposalContentAssessmentCommandValidator>();
        services.AddScoped<ConfirmProposalContentAssessmentCommandValidator>();

        services.AddScoped<IRequestValidator<CorrectProposalContentAssessmentCommand>, CorrectProposalContentAssessmentCommandValidator>();
        services.AddScoped<CorrectProposalContentAssessmentCommandValidator>();

        return services;
    }
}
