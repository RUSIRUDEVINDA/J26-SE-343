namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure.Persistence;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StateLandGovernance.Shared.Infrastructure.Configuration;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;
using Xunit;

public sealed class WorkflowGovernancePostgresIntegrationTests
{
    private static string? GetConnectionString()
    {
        EnvFileLoader.LoadFromRepositoryRoot();
        return Environment.GetEnvironmentVariable("WORKFLOW_GOVERNANCE_CONNECTION")
            ?? Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION");
    }

    private static ServiceProvider CreateServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddWorkflowGovernancePostgresPersistence(connectionString);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RestartPersistence_AllAggregatesAndHistorySurviveProviderRestart()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var caseId = new LeaseCaseId(Guid.NewGuid());
        var appRef = $"SLG-APP-{Guid.NewGuid():N}"[..25];
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var ver1Id = new DocumentVersionId(Guid.NewGuid());
        var ver2Id = new DocumentVersionId(Guid.NewGuid());
        var checksum1 = new DocumentChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        var checksum2 = new DocumentChecksum("SHA-256", "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb");
        var contentRef1 = new DocumentContentReference("local://workflow-governance/docs/test1.pdf");
        var contentRef2 = new DocumentContentReference("local://workflow-governance/docs/test2.pdf");

        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var run1Id = new AnalysisRunId(Guid.NewGuid());
        var resultId = new AnalysisRunResultId(Guid.NewGuid());
        var artifactId = new AnalysisResultArtifactId(Guid.NewGuid());
        var factId = new ExtractedFactId(Guid.NewGuid());
        var verificationId = new HumanFactVerificationId(Guid.NewGuid());
        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());

        var assessmentId = new DocumentCompletenessAssessmentId(Guid.NewGuid());

        // --- SESSION 1: STAGE AND COMMIT ALL AGGREGATES ---
        using (var provider1 = CreateServiceProvider(connectionString))
        using (var scope1 = provider1.CreateScope())
        {
            var sp = scope1.ServiceProvider;
            var caseRepo = sp.GetRequiredService<ILeaseCaseRepository>();
            var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
            var assessmentRepo = sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>();
            var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            // 1. LeaseCase aggregate
            var caseAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var leaseCase = new LeaseCase(caseId, appRef, actorId, now, caseAuthority);
            await caseRepo.AddAsync(leaseCase);

            // 2. GovernedDocument aggregate
            var docAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "DocumentSubmitter" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var doc = new GovernedDocument(
                docId, caseId, "Deed", ver1Id, checksum1, contentRef1,
                "deed_v1.pdf", "application/pdf", 1024, actorId, now, docAuthority);

            doc.AddVersion(
                ver2Id, ver1Id, checksum2, contentRef2,
                "deed_v2.pdf", "application/pdf", 2048, actorId, now.AddMinutes(1), docAuthority);

            await docRepo.AddAsync(doc);

            // 3. DocumentAnalysis aggregate
            var analysis = new DocumentAnalysis(analysisId, docId, ver1Id, checksum1, 1, 1, now);
            var modelRef = new AnalysisModelReference("Antigravity", "Qwen-OCR", "1.0");
            var caps = new List<AnalysisCapabilityCode> { new("OCR"), new("KEY_VALUE_EXTRACTION") };

            analysis.RequestRun(run1Id, modelRef, caps, now.AddMinutes(2));
            analysis.StartRun(run1Id, now.AddMinutes(3));

            var artifact = new AnalysisResultArtifactReference(
                artifactId, "ocr_text", "artifacts/run1.txt", "text/plain",
                new AnalysisArtifactChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

            var factInput = new ExtractedFactInput(
                factId,
                new FactCode("LESSEE_NAME"),
                new AnalysisFactValue(AnalysisFactValueKind.Text, "Bandara"),
                new ConfidenceScore(0.95m),
                new AnalysisEvidenceReference(artifactId, 1, "Lessee: Bandara"));

            var factInputs = new List<ExtractedFactInput> { factInput };
            var artifacts = new List<AnalysisResultArtifactReference> { artifact };

            analysis.CompleteRun(run1Id, resultId, AnalysisResultOutcome.OutputsProduced, artifacts, factInputs, now.AddMinutes(4));

            var verifierScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D"));
            var verifierSnapshot = new VerifiedAuthoritySnapshot(
                actorId, new[] { "FactVerifier", "FactSnapshotPublisher" }, verifierScope, now.AddHours(-1), now, now.AddHours(2));

            var extractedFact = analysis.Runs.First().Result!.ExtractedFacts.First();
            analysis.RecordFactVerification(
                verificationId, resultId, extractedFact.Id, FactVerificationDecision.Confirmed,
                correctedValue: null, reason: null, actorId, now.AddMinutes(5), verifierSnapshot);

            analysis.PublishVerifiedFactSnapshot(snapshotId, resultId, actorId, now.AddMinutes(6), verifierSnapshot);

            await analysisRepo.AddAsync(analysis);

            // 4. DocumentCompletenessAssessment aggregate
            var assessedBinding = new AssessedDocumentBinding(docId, ver1Id, checksum1);
            var classifiedDoc = new ClassifiedDocument(
                new ClassifiedDocumentId(Guid.NewGuid()),
                assessedBinding,
                new DocumentClassificationCode("DEED"),
                new ConfidenceScore(0.98m),
                DocumentClassificationStatus.Accepted,
                run1Id,
                resultId,
                modelRef,
                now.AddMinutes(7));

            var req = new DocumentRequirementSnapshot(
                new DocumentRequirementId(Guid.NewGuid()),
                new DocumentClassificationCode("DEED"),
                RequirementCriticality.Mandatory,
                RequirementApplicability.Required,
                1,
                "LEGAL_REQUIREMENT",
                "Primary title deed required");

            var assessment = new DocumentCompletenessAssessment(
                assessmentId, caseId, "LEASING_REQUIREMENTS", "1.0",
                new[] { assessedBinding },
                new[] { classifiedDoc },
                new[] { req },
                now.AddMinutes(8));

            await assessmentRepo.AddAsync(assessment);

            // COMMIT ALL
            var savedCount = await uow.CommitAsync();
            Assert.True(savedCount > 0);
        }

        // --- SESSION 2: NEW PROVIDER (APPLICATION RESTART) ---
        using (var provider2 = CreateServiceProvider(connectionString))
        using (var scope2 = provider2.CreateScope())
        {
            var sp = scope2.ServiceProvider;
            var caseRepo = sp.GetRequiredService<ILeaseCaseRepository>();
            var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
            var assessmentRepo = sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>();

            // 1. Verify LeaseCase
            var loadedCase = await caseRepo.GetByIdAsync(caseId);
            Assert.NotNull(loadedCase);
            Assert.Equal(caseId, loadedCase.Id);
            Assert.Equal(appRef, loadedCase.ApplicationReference);
            Assert.Equal(1, loadedCase.Revision);

            var loadedByRef = await caseRepo.GetByApplicationReferenceAsync(appRef);
            Assert.NotNull(loadedByRef);
            Assert.Equal(caseId, loadedByRef.Id);

            // 2. Verify GovernedDocument
            var loadedDoc = await docRepo.GetByIdAsync(docId);
            Assert.NotNull(loadedDoc);
            Assert.Equal(docId, loadedDoc.Id);
            Assert.Equal(caseId, loadedDoc.LeaseCaseId);
            Assert.Equal(2, loadedDoc.Versions.Count);
            Assert.Equal(ver2Id, loadedDoc.ActiveVersionId);
            Assert.Equal(2, loadedDoc.Revision);

            var v1 = loadedDoc.Versions.First(v => v.VersionNumber == 1);
            Assert.Equal(ver1Id, v1.Id);
            Assert.Equal("deed_v1.pdf", v1.OriginalFileName);
            Assert.Equal("local://workflow-governance/docs/test1.pdf", v1.ContentReference.Value);

            var v2 = loadedDoc.Versions.First(v => v.VersionNumber == 2);
            Assert.Equal(ver2Id, v2.Id);
            Assert.Equal("deed_v2.pdf", v2.OriginalFileName);
            Assert.Equal("local://workflow-governance/docs/test2.pdf", v2.ContentReference.Value);

            var docsByCase = await docRepo.GetByLeaseCaseIdAsync(caseId);
            Assert.Single(docsByCase);

            // 3. Verify DocumentAnalysis
            var loadedAnalysis = await analysisRepo.GetByIdAsync(analysisId);
            Assert.NotNull(loadedAnalysis);
            Assert.Equal(analysisId, loadedAnalysis.Id);
            Assert.Equal(ver1Id, loadedAnalysis.DocumentVersionId);
            Assert.Single(loadedAnalysis.Runs);

            var run = loadedAnalysis.Runs.First();
            Assert.Equal(AnalysisRunState.Completed, run.State);
            Assert.NotNull(run.Result);
            Assert.Equal(AnalysisResultOutcome.OutputsProduced, run.Result.Outcome);
            Assert.Single(run.Result.Artifacts);
            Assert.Equal("ocr_text", run.Result.Artifacts.First().ArtifactKind);
            Assert.Single(run.Result.ExtractedFacts);
            Assert.Equal("Bandara", run.Result.ExtractedFacts.First().FactValue.CanonicalValue);

            Assert.Single(loadedAnalysis.Verifications);
            Assert.Equal(FactVerificationDecision.Confirmed, loadedAnalysis.Verifications.First().Decision);

            Assert.Single(loadedAnalysis.VerifiedFactSnapshots);
            var snap = loadedAnalysis.VerifiedFactSnapshots.First();
            Assert.Equal(snapshotId, snap.Id);
            Assert.Equal(1, snap.PublishedFactCount);

            // 4. Verify snapshot direct lookup
            var directSnap = await analysisRepo.GetSnapshotByIdAsync(snapshotId);
            Assert.NotNull(directSnap);
            Assert.Equal(snapshotId, directSnap.Id);
            Assert.Equal(1, directSnap.PublishedFactCount);

            // 5. Verify DocumentCompletenessAssessment
            var loadedAssessment = await assessmentRepo.GetByIdAsync(assessmentId);
            Assert.NotNull(loadedAssessment);
            Assert.Equal(assessmentId, loadedAssessment.Id);
            Assert.Equal(caseId, loadedAssessment.LeaseCaseId);
            Assert.Single(loadedAssessment.AssessedDocuments);
            Assert.Single(loadedAssessment.ClassifiedDocuments);
            Assert.Single(loadedAssessment.Requirements);

            var assessmentsByCase = await assessmentRepo.GetByLeaseCaseIdAsync(caseId);
            Assert.Single(assessmentsByCase);
        }
    }

    [Fact]
    public async Task AuthoritativeUniqueConstraint_RejectsDuplicateApplicationReference()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var sharedRef = $"DUP-APP-{Guid.NewGuid():N}"[..25];
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var provider = CreateServiceProvider(connectionString);

        // 1. Insert first lease case
        using (var scope1 = provider.CreateScope())
        {
            var caseRepo = scope1.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var uow = scope1.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var case1Id = new LeaseCaseId(Guid.NewGuid());
            var caseAuthority1 = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, case1Id.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var case1 = new LeaseCase(case1Id, sharedRef, actorId, now, caseAuthority1);
            await caseRepo.AddAsync(case1);
            await uow.CommitAsync();
        }

        // 2. Insert second lease case with duplicate application reference
        using (var scope2 = provider.CreateScope())
        {
            var caseRepo = scope2.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var uow = scope2.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var case2Id = new LeaseCaseId(Guid.NewGuid());
            var caseAuthority2 = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, case2Id.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var case2 = new LeaseCase(case2Id, sharedRef, actorId, now, caseAuthority2);
            await caseRepo.AddAsync(case2);

            var ex = await Assert.ThrowsAsync<DuplicateApplicationReferenceException>(() => uow.CommitAsync());
            Assert.Contains(sharedRef, ex.Message);
        }
    }

    [Fact]
    public async Task OptimisticConcurrency_MismatchedRevisionFailsAndProtectsDatabase()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var caseId = new LeaseCaseId(Guid.NewGuid());
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var provider = CreateServiceProvider(connectionString);

        // 1. Initial insert of LeaseCase (Revision = 1)
        using (var setupScope = provider.CreateScope())
        {
            var caseRepo = setupScope.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var uow = setupScope.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var caseAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var initialCase = new LeaseCase(caseId, $"CASE-{Guid.NewGuid():N}"[..25], actorId, now, caseAuthority);
            await caseRepo.AddAsync(initialCase);
            await uow.CommitAsync();
        }

        // 2. Scope A loads aggregate at Revision 1
        using var scopeA = provider.CreateScope();
        var caseRepoA = scopeA.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
        var uowA = scopeA.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();
        var caseA = await caseRepoA.GetByIdAsync(caseId);
        Assert.NotNull(caseA);
        Assert.Equal(1, caseA.Revision);

        // 3. Scope B loads and updates aggregate to Revision 2 concurrently
        using (var scopeB = provider.CreateScope())
        {
            var caseRepoB = scopeB.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var uowB = scopeB.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var caseB = await caseRepoB.GetByIdAsync(caseId);
            Assert.NotNull(caseB);

            var caseAuthorityB = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));

            var intakeB = new LeaseProposalIntake(
                purpose: LeasePurpose.Agricultural,
                requestedExtent: new LandExtent(5m, MeasurementUnit.Hectare),
                jurisdiction: new JurisdictionContext("CP-KAN", "Central", "Kandy"),
                sourceReference: new IntakeSourceReference("PORTAL-B", "1.0"));

            caseB.RecordProposalIntake(intakeB, actorId, now, caseAuthorityB);
            Assert.Equal(2, caseB.Revision);

            await uowB.CommitAsync();
        }

        // 4. Scope A attempts to mutate based on stale Revision 1
        var caseAuthorityA = new VerifiedAuthoritySnapshot(
            actorId, new[] { "LeaseInitiator" },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
            now.AddHours(-1), now, now.AddHours(2));

        var intakeA = new LeaseProposalIntake(
            purpose: LeasePurpose.Commercial,
            requestedExtent: new LandExtent(10m, MeasurementUnit.Hectare),
            jurisdiction: new JurisdictionContext("CP-KAN", "Central", "Kandy"),
            sourceReference: new IntakeSourceReference("PORTAL-A", "2.0"));

        caseA.RecordProposalIntake(intakeA, actorId, now, caseAuthorityA);

        // Commit should fail with LeaseCaseConcurrencyException (expected 1, actual 2 in DB)
        var concEx = await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => uowA.CommitAsync());
        Assert.Equal(caseId.Value, concEx.LeaseCaseId);

        // 5. Verify no lost update: database still contains Scope B's state
        using (var verifyScope = provider.CreateScope())
        {
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var loadedCase = await verifyRepo.GetByIdAsync(caseId);
            Assert.NotNull(loadedCase);
            Assert.Equal(2, loadedCase.Revision);
            Assert.NotNull(loadedCase.ProposalIntake);
            Assert.Equal("PORTAL-B", loadedCase.ProposalIntake.SourceReference.Reference);
        }
    }

    [Fact]
    public void DocumentStorageIntegrity_BinaryBytesAreNotStoredInDatabase()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var provider = CreateServiceProvider(connectionString);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowGovernanceDbContext>();

        var docVersionEntityType = dbContext.Model.FindEntityType(typeof(StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities.DocumentVersionEntity));
        Assert.NotNull(docVersionEntityType);

        // Verify there is NO byte array property on DocumentVersionEntity
        var byteArrayProps = docVersionEntityType.GetProperties()
            .Where(p => p.ClrType == typeof(byte[]) || p.ClrType == typeof(ReadOnlyMemory<byte>))
            .ToList();

        Assert.Empty(byteArrayProps);

        // Verify storage_path property exists
        var storagePathProp = docVersionEntityType.FindProperty("StoragePath");
        Assert.NotNull(storagePathProp);
        Assert.Equal("storage_path", storagePathProp.GetColumnName());
    }

    [Fact]
    public async Task RealOcrPersistenceRestart_StateInPostgres_And_ArtifactInStorage_SurvivesRestart()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tempDir = Path.Combine(Path.GetTempPath(), "slg-test-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var caseId = new LeaseCaseId(Guid.NewGuid());
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var verId = new DocumentVersionId(Guid.NewGuid());
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var resultId = new AnalysisRunResultId(Guid.NewGuid());
        var artifactId = new AnalysisResultArtifactId(Guid.NewGuid());
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var samplePdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Sri Lanka Land Deed binary content");
        var sampleTranscriptBytes = Encoding.UTF8.GetBytes("Deed of Lease: Land in Hambantota District. All rights transferred.");
        string storageRef;

        try
        {
            // === SESSION 1: CREATE CASE, WRITE CONTENT, REGISTER DOC, RUN ANALYSIS, WRITE ARTIFACT, COMMIT ===
            var services1 = new ServiceCollection();
            services1.AddWorkflowGovernancePostgresPersistence(connectionString);
            services1.AddWorkflowGovernanceLocalDocumentStorage(configureOptions: opt => opt.RootPath = tempDir);
            services1.AddLogging();

            using (var provider1 = services1.BuildServiceProvider())
            using (var scope1 = provider1.CreateScope())
            {
                var sp = scope1.ServiceProvider;
                var caseRepo = sp.GetRequiredService<ILeaseCaseRepository>();
                var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
                var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
                var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();
                var contentWriter = sp.GetRequiredService<IDocumentContentWriter>();
                var artifactWriter = sp.GetRequiredService<IAnalysisArtifactWriter>();

                // 1. Create and add LeaseCase
                var caseAuthority = new VerifiedAuthoritySnapshot(
                    actorId, new[] { "LeaseInitiator" },
                    new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                    now.AddHours(-1), now, now.AddHours(2));
                var leaseCase = new LeaseCase(caseId, $"CASE-{Guid.NewGuid():N}"[..25], actorId, now, caseAuthority);
                await caseRepo.AddAsync(leaseCase);

                // 2. Write document bytes to real local storage
                using var stream = new MemoryStream(samplePdfBytes);
                var receipt = await contentWriter.WriteAsync(stream, "title_deed.pdf", "application/pdf", docId.Value, verId.Value);

                // 3. Register GovernedDocument + DocumentVersion using the real receipt
                var docAuthority = new VerifiedAuthoritySnapshot(
                    actorId, new[] { "DocumentSubmitter" },
                    new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                    now.AddHours(-1), now, now.AddHours(2));
                var docChecksum = new DocumentChecksum("SHA-256", receipt.ChecksumValue);
                var docContentRef = new DocumentContentReference(receipt.ContentReference);
                var governedDoc = new GovernedDocument(
                    docId, caseId, "Deed", verId, docChecksum, docContentRef,
                    receipt.OriginalFileName, receipt.MediaType, receipt.FileSizeInBytes,
                    actorId, now, docAuthority);
                await docRepo.AddAsync(governedDoc);

                // 4. Request DocumentAnalysis
                var analysis = new DocumentAnalysis(analysisId, docId, verId, docChecksum, 1, 1, now);
                analysis.RequestRun(
                    runId,
                    new AnalysisModelReference("FastApi", "Tesseract-5", "5.4.0"),
                    new[] { new AnalysisCapabilityCode("Ocr") },
                    now);
                await analysisRepo.AddAsync(analysis);

                // 5. Commit initial state
                await uow.CommitAsync();

                // 6. Simulate analysis execution: claim run to Running, write artifact through real artifact store, complete run
                analysis.StartRun(runId, now.AddSeconds(1));

                var artifactKey = $"artifacts/{analysisId.Value:D}/{runId.Value:D}/ocr_transcript.txt";
                var writeReceipt = await artifactWriter.WriteAsync(artifactKey, "text/plain", sampleTranscriptBytes);
                storageRef = writeReceipt.StorageReference;

                var artifactRef = new AnalysisResultArtifactReference(
                    artifactId,
                    "ocr_transcript",
                    writeReceipt.StorageReference,
                    "text/plain",
                    new AnalysisArtifactChecksum("SHA-256", writeReceipt.ChecksumValue));

                // Zero candidate facts (OCR-only slice invariant)
                analysis.CompleteRun(
                    runId,
                    resultId,
                    AnalysisResultOutcome.OutputsProduced,
                    new[] { artifactRef },
                    Array.Empty<ExtractedFactInput>(),
                    now.AddSeconds(2));

                await uow.CommitAsync();
            }

            // === SESSION 2: NEW SERVICE PROVIDER (FULL RESTART) ===
            var services2 = new ServiceCollection();
            services2.AddWorkflowGovernancePostgresPersistence(connectionString);
            services2.AddWorkflowGovernanceLocalDocumentStorage(configureOptions: opt => opt.RootPath = tempDir);
            services2.AddLogging();

            using (var provider2 = services2.BuildServiceProvider())
            using (var scope2 = provider2.CreateScope())
            {
                var sp = scope2.ServiceProvider;
                var caseRepo2 = sp.GetRequiredService<ILeaseCaseRepository>();
                var docRepo2 = sp.GetRequiredService<IGovernedDocumentRepository>();
                var analysisRepo2 = sp.GetRequiredService<IDocumentAnalysisRepository>();
                var artifactReader = sp.GetRequiredService<IAnalysisArtifactReader>();
                var contentReader = sp.GetRequiredService<IDocumentContentReader>();

                // 1. Verify aggregate metadata reloaded from PostgreSQL
                var reloadedCase = await caseRepo2.GetByIdAsync(caseId);
                Assert.NotNull(reloadedCase);
                Assert.Equal(caseId, reloadedCase.Id);

                var reloadedDoc = await docRepo2.GetByIdAsync(docId);
                Assert.NotNull(reloadedDoc);
                var reloadedVersion = reloadedDoc.Versions.First();
                Assert.Equal(verId, reloadedVersion.Id);
                Assert.Equal("title_deed.pdf", reloadedVersion.OriginalFileName);
                Assert.Equal(samplePdfBytes.Length, reloadedVersion.FileSizeInBytes);

                var reloadedAnalysis = await analysisRepo2.GetByIdAsync(analysisId);
                Assert.NotNull(reloadedAnalysis);
                var reloadedRun = reloadedAnalysis.Runs.First(r => r.Id == runId);
                Assert.Equal(AnalysisRunState.Completed, reloadedRun.State);
                Assert.NotNull(reloadedRun.Result);
                Assert.Equal(AnalysisResultOutcome.OutputsProduced, reloadedRun.Result.Outcome);
                Assert.Empty(reloadedRun.Result.ExtractedFacts); // ZERO semantic facts
                Assert.Single(reloadedRun.Result.Artifacts);

                var reloadedArtifact = reloadedRun.Result.Artifacts.First();
                Assert.Equal("ocr_transcript", reloadedArtifact.ArtifactKind);
                Assert.Equal(storageRef, reloadedArtifact.StorageReference);

                // 2. Verify source document bytes streamed back through IDocumentContentReader
                var sourceStream = await contentReader.ReadContentAsync(docId, verId, reloadedVersion.ContentReference.Value);
                Assert.NotNull(sourceStream);
                using var ms = new MemoryStream();
                await sourceStream.CopyToAsync(ms);
                Assert.Equal(samplePdfBytes, ms.ToArray());

                // 3. Verify OCR transcript artifact read back through IAnalysisArtifactReader
                var artifactBytes = await artifactReader.ReadAsync(reloadedArtifact.StorageReference);
                Assert.NotNull(artifactBytes);
                Assert.Equal(sampleTranscriptBytes, artifactBytes.Value.ToArray());
                var transcriptText = Encoding.UTF8.GetString(artifactBytes.Value.Span);
                Assert.Contains("Deed of Lease: Land in Hambantota District", transcriptText);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    [Fact]
    public async Task RehydrationSafety_RoundTripPreservesAllInvariantsAndRaisesZeroUnexpectedDomainEvents()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var caseId = new LeaseCaseId(Guid.NewGuid());
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var verId = new DocumentVersionId(Guid.NewGuid());
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var resultId = new AnalysisRunResultId(Guid.NewGuid());
        var artifactId = new AnalysisResultArtifactId(Guid.NewGuid());
        var factId = new ExtractedFactId(Guid.NewGuid());
        var snapshotId = Guid.NewGuid();
        var verificationId = new HumanFactVerificationId(Guid.NewGuid());
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var checksum = new DocumentChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        var contentRef = new DocumentContentReference("documents/test/rehydration.pdf");

        using var provider1 = CreateServiceProvider(connectionString);

        // 1. Arrange & Persist Aggregates in Session 1
        using (var scope1 = provider1.CreateScope())
        {
            var caseRepo = scope1.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
            var docRepo = scope1.ServiceProvider.GetRequiredService<IGovernedDocumentRepository>();
            var analysisRepo = scope1.ServiceProvider.GetRequiredService<IDocumentAnalysisRepository>();
            var uow = scope1.ServiceProvider.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            // LeaseCase with intake, screening, and snapshot linkage (Revision = 2)
            var caseAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "LeaseInitiator" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));
            var leaseCase = new LeaseCase(caseId, $"CASE-{Guid.NewGuid():N}"[..25], actorId, now, caseAuthority);
            leaseCase.RecordProposalIntake(
                new LeaseProposalIntake(
                    LeasePurpose.Agricultural,
                    new LandExtent(5m, MeasurementUnit.Hectare),
                    new JurisdictionContext("CP-KAN", "Central", "Kandy"),
                    new IntakeSourceReference("PORTAL-1", "1.0")),
                actorId, now, caseAuthority);
            leaseCase.SetCurrentVerifiedFactSnapshot(snapshotId);
            leaseCase.RecordScreeningResult(
                new ScreeningResult(Guid.NewGuid(), caseId, snapshotId, ScreeningOutcome.Cleared, "Screening clear", now));
            await caseRepo.AddAsync(leaseCase);

            // GovernedDocument with initial version (Revision = 1)
            var docAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "DocumentSubmitter" },
                new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
                now.AddHours(-1), now, now.AddHours(2));
            var governedDoc = new GovernedDocument(
                docId, caseId, "Deed", verId, checksum, contentRef,
                "rehydration.pdf", "application/pdf", 2048, actorId, now, docAuthority);
            await docRepo.AddAsync(governedDoc);

            // DocumentAnalysis with run, verification, and verified fact snapshot (Revision = 5)
            var analysis = new DocumentAnalysis(analysisId, docId, verId, checksum, 1, 1, now);
            analysis.RequestRun(
                runId,
                new AnalysisModelReference("FastApi", "Tesseract-5", "5.4.0"),
                new[] { new AnalysisCapabilityCode("Ocr") },
                now);
            analysis.StartRun(runId, now.AddSeconds(1));

            var factInput = new ExtractedFactInput(
                factId,
                new FactCode("LESSEE_NAME"),
                new AnalysisFactValue(AnalysisFactValueKind.Text, "Saman Perera"),
                new ConfidenceScore(0.95m),
                new AnalysisEvidenceReference(artifactId, 1, "Lessee: Saman"));

            var artifactRef = new AnalysisResultArtifactReference(
                artifactId,
                "ocr_transcript",
                "artifacts/test.txt",
                "text/plain",
                new AnalysisArtifactChecksum("SHA-256", "abc"));

            analysis.CompleteRun(
                runId,
                resultId,
                AnalysisResultOutcome.OutputsProduced,
                new[] { artifactRef },
                new[] { factInput },
                now.AddSeconds(2));

            var verifierAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "FactVerifier" },
                new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D")),
                now.AddHours(-1), now, now.AddHours(2));

            var extractedFact = analysis.Runs.First().Result!.ExtractedFacts.First();
            analysis.RecordFactVerification(
                verificationId, resultId, extractedFact.Id, FactVerificationDecision.Confirmed,
                null, null, actorId, now.AddSeconds(3), verifierAuthority);

            var publisherAuthority = new VerifiedAuthoritySnapshot(
                actorId, new[] { "FactSnapshotPublisher" },
                new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D")),
                now.AddHours(-1), now, now.AddHours(2));

            analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(snapshotId), resultId, actorId, now.AddSeconds(4), publisherAuthority);

            await analysisRepo.AddAsync(analysis);
            await uow.CommitAsync();
        }

        // 2. Act: Reload across a brand-new ServiceProvider / DbContext
        using var provider2 = CreateServiceProvider(connectionString);
        using var scope2 = provider2.CreateScope();
        var caseRepo2 = scope2.ServiceProvider.GetRequiredService<ILeaseCaseRepository>();
        var docRepo2 = scope2.ServiceProvider.GetRequiredService<IGovernedDocumentRepository>();
        var analysisRepo2 = scope2.ServiceProvider.GetRequiredService<IDocumentAnalysisRepository>();

        var reloadedCase = await caseRepo2.GetByIdAsync(caseId);
        var reloadedDoc = await docRepo2.GetByIdAsync(docId);
        var reloadedAnalysis = await analysisRepo2.GetByIdAsync(analysisId);

        // 3. Assert: All invariants, IDs, revisions, and state preserved
        Assert.NotNull(reloadedCase);
        Assert.Equal(caseId, reloadedCase.Id);
        Assert.Equal(2, reloadedCase.Revision);
        Assert.Equal(snapshotId, reloadedCase.CurrentVerifiedFactSnapshotId);
        Assert.NotNull(reloadedCase.ProposalIntake);
        Assert.Equal("PORTAL-1", reloadedCase.ProposalIntake.SourceReference.Reference);
        Assert.NotNull(reloadedCase.LatestScreening);
        Assert.Equal(ScreeningOutcome.Cleared, reloadedCase.LatestScreening.Outcome);

        Assert.NotNull(reloadedDoc);
        Assert.Equal(docId, reloadedDoc.Id);
        Assert.Equal(1, reloadedDoc.Revision);
        Assert.Single(reloadedDoc.Versions);
        Assert.Equal(verId, reloadedDoc.Versions.First().Id);

        Assert.NotNull(reloadedAnalysis);
        Assert.Equal(analysisId, reloadedAnalysis.Id);
        Assert.Equal(6, reloadedAnalysis.Revision);
        Assert.Single(reloadedAnalysis.Runs);
        Assert.Single(reloadedAnalysis.Verifications);
        Assert.Single(reloadedAnalysis.VerifiedFactSnapshots);
        Assert.Equal(snapshotId, reloadedAnalysis.VerifiedFactSnapshots.First().Id.Value);

        // 4. CRITICAL: Zero spurious DomainEvents raised during rehydration
        Assert.Empty(reloadedCase.DomainEvents);
        Assert.Empty(reloadedDoc.DomainEvents);
        Assert.Empty(reloadedAnalysis.DomainEvents);
    }

    [Fact]
    public async Task DatabaseMetadata_VerifiesMigrationsHistoryAndSchemaTables()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var provider = CreateServiceProvider(connectionString);
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowGovernanceDbContext>();

        // 1. Inspect __ef_migrations_history in schema workflow_governance
        var appliedMigrations = await dbContext.Database
            .SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM workflow_governance.__ef_migrations_history")
            .ToListAsync();

        Assert.NotEmpty(appliedMigrations);
        Assert.Contains("20261010100732_InitialWorkflowGovernancePersistence", appliedMigrations);

        // 2. Query all tables in workflow_governance
        var tables = await dbContext.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'workflow_governance'")
            .ToListAsync();

        Assert.Contains("__ef_migrations_history", tables);
        Assert.Contains("lease_cases", tables);
        Assert.Contains("governed_documents", tables);
        Assert.Contains("document_versions", tables);
        Assert.Contains("document_analyses", tables);
        Assert.Contains("analysis_runs", tables);
        Assert.Contains("human_fact_verifications", tables);
        Assert.Contains("verified_fact_snapshots", tables);
        Assert.Contains("document_completeness_assessments", tables);
        Assert.Contains("workflow_plans", tables);
        Assert.Contains("workflow_executions", tables);
        Assert.Contains("institutional_routing_configs", tables);

        // 3. Assert non-secret metadata from connection string
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.False(string.IsNullOrWhiteSpace(builder.Host));
        Assert.True(builder.Port > 0);
        Assert.Equal("state_land_governance", builder.Database);
        Assert.Equal("postgres", builder.Username);
    }

    [Fact]
    public void DurableRepositoryResolution_And_LocalStorageCoexistence()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkflowGovernancePostgresPersistence(connectionString);
        services.AddWorkflowGovernanceInfrastructure(allowInMemoryLeasePersistence: true);
        services.AddWorkflowGovernanceLocalDocumentStorage(configureOptions: opt => opt.RootPath = ".local-data/workflow-governance");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // 1. Confirm durable PostgreSQL implementations resolve
        var caseRepo = sp.GetRequiredService<ILeaseCaseRepository>();
        var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
        var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
        var assessmentRepo = sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>();
        var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

        Assert.IsType<PostgresLeaseCaseRepository>(caseRepo);
        Assert.IsType<PostgresGovernedDocumentRepository>(docRepo);
        Assert.IsType<PostgresDocumentAnalysisRepository>(analysisRepo);
        Assert.IsType<PostgresDocumentCompletenessAssessmentRepository>(assessmentRepo);
        Assert.IsType<PostgresWorkflowGovernanceUnitOfWork>(uow);

        // 2. Confirm none resolve to in-memory demo implementations
        Assert.IsNotType<InMemoryLeaseCaseRepository>(caseRepo);
        Assert.IsNotType<InMemoryGovernedDocumentRepository>(docRepo);
        Assert.IsNotType<InMemoryDocumentAnalysisRepository>(analysisRepo);
        Assert.IsNotType<InMemoryWorkflowGovernanceUnitOfWork>(uow);

        // 3. Confirm coexistence with local development storage
        var contentWriter = sp.GetRequiredService<IDocumentContentWriter>();
        var contentReader = sp.GetRequiredService<IDocumentContentReader>();
        var artifactWriter = sp.GetRequiredService<IAnalysisArtifactWriter>();
        var artifactReader = sp.GetRequiredService<IAnalysisArtifactReader>();

        Assert.IsType<LocalFileDocumentContentStore>(contentWriter);
        Assert.IsType<LocalFileDocumentContentStore>(contentReader);
        Assert.IsType<LocalFileAnalysisArtifactStore>(artifactWriter);
        Assert.IsType<LocalFileAnalysisArtifactStore>(artifactReader);
    }

    [Fact]
    public async Task SemanticFactLifecycle_PostgresPersistenceRestart_SurvivesSessionDisposalAndExcludesUnsupported()
    {
        var connectionString = GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var now = DateTime.UtcNow;
        var officerId = Guid.NewGuid();
        var caseId = new LeaseCaseId(Guid.NewGuid());
        var appRef = $"SLG-APP-{Guid.NewGuid():N}"[..25];
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());
        var checksum = new DocumentChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        var contentRef = new DocumentContentReference("local://workflow-governance/docs/proposal.pdf");

        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var resultId = new AnalysisRunResultId(Guid.NewGuid());
        var fact1Id = new ExtractedFactId(Guid.NewGuid());
        var fact2Id = new ExtractedFactId(Guid.NewGuid());
        var fact3Id = new ExtractedFactId(Guid.NewGuid());
        var verification1Id = new HumanFactVerificationId(Guid.NewGuid());
        var verification2Id = new HumanFactVerificationId(Guid.NewGuid());
        var verification3Id = new HumanFactVerificationId(Guid.NewGuid());
        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());

        var caseAuthority = new VerifiedAuthoritySnapshot(
            officerId,
            new[] { "LeaseInitiator" },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
            now.AddHours(-1), now, now.AddHours(2));

        var docAuthority = new VerifiedAuthoritySnapshot(
            officerId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString()),
            now.AddHours(-1), now, now.AddHours(2));

        var verifierAuthority = new VerifiedAuthoritySnapshot(
            officerId,
            new[] { "FactVerifier", "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D")),
            now.AddHours(-1),
            now.AddMinutes(-5),
            now.AddHours(2));

        // --- SESSION 1: STAGE AND COMMIT DOCUMENT ANALYSIS WITH MACHINE CANDIDATE FACTS ---
        using (var provider1 = CreateServiceProvider(connectionString))
        using (var scope1 = provider1.CreateScope())
        {
            var sp = scope1.ServiceProvider;
            var caseRepo = sp.GetRequiredService<ILeaseCaseRepository>();
            var docRepo = sp.GetRequiredService<IGovernedDocumentRepository>();
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
            var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var leaseCase = new LeaseCase(caseId, appRef, officerId, now, caseAuthority);
            await caseRepo.AddAsync(leaseCase);

            var doc = new GovernedDocument(docId, caseId, "LeaseProposal", versionId, checksum, contentRef, "proposal.pdf", "application/pdf", 1024, officerId, now, docAuthority);
            await docRepo.AddAsync(doc);

            var analysis = new DocumentAnalysis(analysisId, docId, versionId, checksum, 1, 1, now);
            analysis.RequestRun(
                runId,
                new AnalysisModelReference("FastApi-QwenSemantic", "Qwen/Qwen2.5-7B-Instruct", "qwen-proposal-extraction-v1"),
                new[] { new AnalysisCapabilityCode("Ocr"), new AnalysisCapabilityCode("SemanticExtraction") },
                now.AddMinutes(1));
            analysis.StartRun(runId, now.AddMinutes(2));

            var artifacts = new List<AnalysisResultArtifactReference>
            {
                new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "OcrDocumentTranscript", "analysis-artifacts/test/doc.txt", "text/plain", new AnalysisArtifactChecksum("SHA-256", "hash1")),
                new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "OcrPageTranscript", "analysis-artifacts/test/page1.json", "application/json", new AnalysisArtifactChecksum("SHA-256", "hash2")),
                new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "SemanticRawResponse", "analysis-artifacts/test/sem.json", "application/json", new AnalysisArtifactChecksum("SHA-256", "hash3"))
            };

            var facts = new List<ExtractedFactInput>
            {
                new ExtractedFactInput(fact1Id, new FactCode("applicant_name"), new AnalysisFactValue(AnalysisFactValueKind.Text, "Lanka Bio Tech Ltd"), new ConfidenceScore(0.95m), null),
                new ExtractedFactInput(fact2Id, new FactCode("land_extent"), new AnalysisFactValue(AnalysisFactValueKind.Decimal, "15.5"), new ConfidenceScore(0.88m), null),
                new ExtractedFactInput(fact3Id, new FactCode("investment_amount"), new AnalysisFactValue(AnalysisFactValueKind.Integer, "500000000"), new ConfidenceScore(0.30m), null)
            };

            analysis.CompleteRun(runId, resultId, AnalysisResultOutcome.OutputsProduced, artifacts, facts, now.AddMinutes(3));
            await analysisRepo.AddAsync(analysis);
            await uow.CommitAsync();
        }

        // --- SESSION 2: RELOAD FROM POSTGRES, PERFORM HUMAN VERIFICATIONS, COMMIT ---
        using (var provider2 = CreateServiceProvider(connectionString))
        using (var scope2 = provider2.CreateScope())
        {
            var sp = scope2.ServiceProvider;
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
            var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var analysis = await analysisRepo.GetByIdAsync(analysisId);
            Assert.NotNull(analysis);
            var run = analysis.Runs.Single(r => r.Id == runId);
            Assert.Equal(AnalysisRunState.Completed, run.State);
            Assert.Equal(3, run.Result!.ExtractedFacts.Count);
            Assert.Equal(3, run.Result.Artifacts.Count);

            // Officer verifications
            // 1. Confirmed
            analysis.RecordFactVerification(verification1Id, resultId, fact1Id, FactVerificationDecision.Confirmed, null, null, officerId, now.AddMinutes(4), verifierAuthority);
            // 2. Corrected
            analysis.RecordFactVerification(verification2Id, resultId, fact2Id, FactVerificationDecision.Corrected, new AnalysisFactValue(AnalysisFactValueKind.Decimal, "12.5"), "Survey plan confirms 12.5 acres", officerId, now.AddMinutes(5), verifierAuthority);
            // 3. Unsupported
            analysis.RecordFactVerification(verification3Id, resultId, fact3Id, FactVerificationDecision.Unsupported, null, "Unsubstantiated projection", officerId, now.AddMinutes(6), verifierAuthority);

            await uow.CommitAsync();
        }

        // --- SESSION 3: RELOAD FROM POSTGRES, PUBLISH SNAPSHOT, COMMIT ---
        using (var provider3 = CreateServiceProvider(connectionString))
        using (var scope3 = provider3.CreateScope())
        {
            var sp = scope3.ServiceProvider;
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();
            var uow = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();

            var analysis = await analysisRepo.GetByIdAsync(analysisId);
            Assert.NotNull(analysis);
            Assert.Equal(3, analysis.Verifications.Count);

            analysis.PublishVerifiedFactSnapshot(snapshotId, resultId, officerId, now.AddMinutes(7), verifierAuthority);
            await uow.CommitAsync();
        }

        // --- SESSION 4: RELOAD FROM POSTGRES, VERIFY PERSISTED SNAPSHOT EXCLUDES UNSUPPORTED ---
        using (var provider4 = CreateServiceProvider(connectionString))
        using (var scope4 = provider4.CreateScope())
        {
            var sp = scope4.ServiceProvider;
            var analysisRepo = sp.GetRequiredService<IDocumentAnalysisRepository>();

            var analysis = await analysisRepo.GetByIdAsync(analysisId);
            Assert.NotNull(analysis);
            Assert.Single(analysis.VerifiedFactSnapshots);

            var snapshot = analysis.VerifiedFactSnapshots.Single();
            Assert.Equal(snapshotId, snapshot.Id);
            Assert.Equal(3, snapshot.SourceFactCount);
            Assert.Equal(2, snapshot.PublishedFactCount);
            Assert.Equal(1, snapshot.ConfirmedFactCount);
            Assert.Equal(1, snapshot.CorrectedFactCount);
            Assert.Equal(1, snapshot.UnsupportedFactCount);
            Assert.Equal(2, snapshot.Entries.Count);

            var confirmed = Assert.Single(snapshot.Entries, f => f.FactCode.Value == "applicant_name");
            Assert.Equal("Lanka Bio Tech Ltd", confirmed.EffectiveValue.CanonicalValue);
            Assert.Equal(FactVerificationDecision.Confirmed, confirmed.Decision);

            var corrected = Assert.Single(snapshot.Entries, f => f.FactCode.Value == "land_extent");
            Assert.Equal("12.5", corrected.EffectiveValue.CanonicalValue);
            Assert.Equal(FactVerificationDecision.Corrected, corrected.Decision);

            // Unsupported fact was cleanly excluded from snapshot entries!
            Assert.DoesNotContain(snapshot.Entries, f => f.FactCode.Value == "investment_amount");
        }
    }
}
