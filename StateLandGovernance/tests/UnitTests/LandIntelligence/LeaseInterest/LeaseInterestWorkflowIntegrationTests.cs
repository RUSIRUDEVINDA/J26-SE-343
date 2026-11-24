using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

[Trait("Category", "Integration")]
public sealed class LeaseInterestWorkflowIntegrationTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private readonly Guid _applicantId = Guid.NewGuid();
    private readonly Guid _officerId = Guid.NewGuid();
    private readonly List<Guid> _createdParcelIds = [];
    private string? _testConnectionString;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable(
            "LEASE_INTEREST_NIC_ENCRYPTION_KEY",
            "iywCA5pMwe+7yI4iAexqaf0gtSWGcUQrjynsw3VSquA=");
        Environment.SetEnvironmentVariable(
            "LEASE_INTEREST_NIC_LOOKUP_KEY",
            "DnzU7bYHy9HbizdNuixJVKbvQNMG8zXiCuKQVQ4DJFA=");

        _testConnectionString = LandIntelligenceIntegrationTestConnection.TryGetValidatedConnectionString();
        if (_testConnectionString is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION", _testConnectionString);

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLandIntelligenceInfrastructure(configuration);
        services.AddLandIntelligenceApplication();
        services.AddSingleton<ILandIntelligenceCallerContext>(
            new MutableCallerContext(_applicantId));
        services.AddSingleton<ILeaseInterestOfficerActorContext>(
            new MutableOfficerActorContext(_officerId));

        _serviceProvider = services.BuildServiceProvider();

        var db = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            try
            {
                var db = _serviceProvider.GetRequiredService<LandIntelligenceDbContext>();
                await LeaseInterestWorkflowIntegrationTestCleanup.RemoveTestParcelsAsync(
                    db,
                    _createdParcelIds);
            }
            catch
            {
                // Best-effort cleanup; do not mask test failures.
            }

            await _serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task Applicant_submit_officer_review_applicant_sees_outcome_and_idempotent_replay()
    {
        if (_testConnectionString is null || _serviceProvider is null)
        {
            return;
        }

        Assert.NotNull(_serviceProvider);
        var parcels = _serviceProvider.GetRequiredService<ILandParcelRepository>();
        var applicant = _serviceProvider.GetRequiredService<ILeaseInterestRequestService>();
        var officer = _serviceProvider.GetRequiredService<ILeaseInterestOfficerReviewService>();

        var cadastral =
            $"{LeaseInterestWorkflowIntegrationTestCleanup.TestParcelCadastralPrefix}{Guid.NewGuid():N}"[..24];
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastral, "E2E-SURVEY"),
            new LandCategory(LandCategoryType.StateLand, "E2E lease interest"),
            new LandArea(1.5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS", "GN-E2E"),
            new SpatialReference(6.93, 79.86, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "E2E"),
            new LandCharacteristics("Loam", "Flat", 10m));
        _createdParcelIds.Add(parcel.Id);
        await parcels.AddAsync(parcel);

        var draftDetail = await applicant.GetDraftOrCreateAsync(parcel.Id, CancellationToken.None);
        var submitBody = ValidSubmitDraft();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var submitted = await applicant.SubmitAsync(
            draftDetail.Id,
            submitBody,
            idempotencyKey,
            CancellationToken.None);

        Assert.Equal(LeaseInterestRequestStatus.PendingVerification, submitted.Status);

        var replay = await applicant.SubmitAsync(
            draftDetail.Id,
            submitBody,
            idempotencyKey,
            CancellationToken.None);
        Assert.Equal(submitted.Id, replay.Id);

        var reviewed = await officer.UpdateStatusAsync(
            submitted.Id,
            new LeaseInterestOfficerStatusUpdateDto(
                LeaseInterestRequestStatus.NeedsInformation,
                "Please confirm your postal address via official channels.",
                true),
            CancellationToken.None);

        Assert.Equal(LeaseInterestRequestStatus.NeedsInformation, reviewed.Status);

        var applicantView = await applicant.GetByIdAsync(submitted.Id, CancellationToken.None);
        Assert.Equal(LeaseInterestRequestStatus.NeedsInformation, applicantView.Status);
        Assert.Contains(
            applicantView.StatusHistory,
            h => h.Status == LeaseInterestRequestStatus.NeedsInformation
                && h.Note != null
                && h.Note.Contains("postal address", StringComparison.OrdinalIgnoreCase));
    }

    private static UpsertLeaseInterestDraftDto ValidSubmitDraft() =>
        new(
            new LeaseInterestApplicantDto(
                LeaseInterestApplicantKindDto.Individual,
                "E2E Applicant",
                "+94111222333",
                "e2e.applicant@example.com",
                "123 Test Lane, Colombo",
                null,
                null,
                "199512345678",
                null),
            new LeaseInterestProposalDto(
                LandUseType.Agricultural,
                "Pilot crop trial",
                1.0m,
                "2026-Q3",
                5,
                "Preliminary interest for workflow test."),
            new LeaseInterestSupportingInfoDto("Workflow test supporting notes."),
            true);

    private sealed class MutableCallerContext(Guid ownerId) : ILandIntelligenceCallerContext
    {
        public Guid? OwnerId => ownerId;
    }

    private sealed class MutableOfficerActorContext(Guid officerId) : ILeaseInterestOfficerActorContext
    {
        public Guid? OfficerActorId => officerId;
    }
}
