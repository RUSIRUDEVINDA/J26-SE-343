using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class ResearchSimulatedLeaseReadRepository : IResearchSimulatedLeaseReadRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public ResearchSimulatedLeaseReadRepository(LandIntelligenceDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<ResearchSimulatedLeaseWorkflowDto?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    "ResearchRecordId",
                    "ScenarioId",
                    "SimulatedLeaseStatus",
                    "IsSimulatedLeaseStatus",
                    "DatasetUsage",
                    "Notice"
                FROM land_intelligence.research_simulated_lease_workflows
                WHERE "LandParcelId" = @parcel_id
                """;
            command.Parameters.Add(new NpgsqlParameter("parcel_id", landParcelId));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ResearchSimulatedLeaseWorkflowDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return null;
        }
    }
}
