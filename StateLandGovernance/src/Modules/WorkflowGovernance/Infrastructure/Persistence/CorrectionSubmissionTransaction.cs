namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

/// <summary>Begins after file storage; no OCR or external calls occur inside this transaction.</summary>
public sealed class PostgresCorrectionSubmissionTransaction(WorkflowGovernanceDbContext db) : ICorrectionSubmissionTransaction
{
    public async Task<CorrectionRecord> ExecuteAsync(Func<Task<CorrectionRecord>> action, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

/// <summary>Development/Testing only; offers no durable transactional guarantee.</summary>
public sealed class InMemoryCorrectionSubmissionTransaction : ICorrectionSubmissionTransaction
{
    public Task<CorrectionRecord> ExecuteAsync(Func<Task<CorrectionRecord>> action, CancellationToken cancellationToken) => action();
}
