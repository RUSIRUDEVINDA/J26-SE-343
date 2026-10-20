// ISOLATED TEST FIXTURE ONLY. No normal-host case or existing completed record is changed.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

internal static class ConcurrencyPersistenceProof
{
    public static async Task RunAsync(IServiceProvider provider, bool write)
    {
        var id = Guid.Parse("fac50000-0000-4000-8000-000000000001");
        if (write)
        {
            using (var setup = provider.CreateScope())
            {
                var repository = setup.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
                if (await repository.GetByIdAsync(new(id)) != null) throw new InvalidOperationException("Concurrency fixture exists; use --extensions-concurrency-read.");
                var actor = Guid.NewGuid(); var at = DateTime.UtcNow;
                var authority = new VerifiedAuthoritySnapshot(actor, ["LeaseInitiator"], new(AuthorityScopeKind.LeaseCase, id.ToString("D")), at.AddMinutes(-1), at, at.AddHours(1));
                await repository.AddAsync(new LeaseCase(new(id), "ISOLATED-CONCURRENCY-20261017", actor, at, authority));
                await setup.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
            }
            using var left = provider.CreateScope(); using var right = provider.CreateScope();
            var a = (await left.ServiceProvider.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(id)))!;
            var b = (await right.ServiceProvider.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(id)))!;
            a.AddDocumentSubmissionRequirement(new("ISOLATED_TEST_WINNER"), DateTime.UtcNow.AddDays(1));
            b.AddDocumentSubmissionRequirement(new("ISOLATED_TEST_LOSER"), DateTime.UtcNow.AddDays(1));
            await left.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync();
            var rejected = false;
            try { await right.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>().CommitAsync(); }
            catch (LeaseCaseConcurrencyException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Database accepted a lost update.");
        }
        using var read = provider.CreateScope();
        var stored = await read.ServiceProvider.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(id))
            ?? throw new InvalidOperationException("Run --extensions-concurrency-write first.");
        if (stored.DocumentSubmissionRequirements.Count != 1 || stored.DocumentSubmissionRequirements.Single().DocumentClassificationCode.Value != "ISOLATED_TEST_WINNER")
            throw new InvalidOperationException("Winning update was not preserved independently of the rejected transaction.");
        Console.WriteLine(JsonSerializer.Serialize(new { fixture = "ISOLATED TEST ONLY", database = "workflow_execution_20261016",
            caseId = id, mode = write ? "two-independent-scopes-stale-transaction-rejected" : "fresh-process-concurrency-result-retrieval",
            stored.Revision, status = stored.Status.ToString(), winningRequirementId = stored.DocumentSubmissionRequirements.Single().Id,
            survivingRequirements = stored.DocumentSubmissionRequirements.Count, lostUpdateRejected = true }));
    }
}
