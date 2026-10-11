using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class ProjectLeaseReadyDesignationReadRepository : IProjectLeaseReadyDesignationReadRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public ProjectLeaseReadyDesignationReadRepository(LandIntelligenceDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<ProjectLeaseReadyDesignationRecord?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            """
            SELECT "ResearchRecordId", "LandParcelId", "DatasetKey"
            FROM land_intelligence.colombo_state_research_project_lease_ready
            WHERE "LandParcelId" = @parcel_id
            """,
            [new NpgsqlParameter("parcel_id", landParcelId)],
            cancellationToken);

        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectLeaseReadyDesignationRecord>> GetByParcelIdsAsync(
        IReadOnlyList<Guid> landParcelIds,
        CancellationToken cancellationToken = default)
    {
        if (landParcelIds.Count == 0)
        {
            return new Dictionary<Guid, ProjectLeaseReadyDesignationRecord>();
        }

        var rows = await QueryAsync(
            """
            SELECT "ResearchRecordId", "LandParcelId", "DatasetKey"
            FROM land_intelligence.colombo_state_research_project_lease_ready
            WHERE "LandParcelId" = ANY(@parcel_ids)
            """,
            [new NpgsqlParameter("parcel_ids", landParcelIds.ToArray())],
            cancellationToken);

        return rows.ToDictionary(row => row.LandParcelId);
    }

    public async Task<IReadOnlyList<ProjectLeaseReadyDesignationRecord>> ListDesignatedAsync(
        CancellationToken cancellationToken = default) =>
        await QueryAsync(
            """
            SELECT "ResearchRecordId", "LandParcelId", "DatasetKey"
            FROM land_intelligence.colombo_state_research_project_lease_ready
            ORDER BY "ResearchRecordId"
            """,
            [],
            cancellationToken);

    public async Task<int> CountDesignatedAsync(CancellationToken cancellationToken = default) =>
        (await ListDesignatedAsync(cancellationToken)).Count;

    public Task<int> CountTotalParcelsAsync(CancellationToken cancellationToken = default) =>
        _dbContext.LandParcels.CountAsync(cancellationToken);

    private async Task<List<ProjectLeaseReadyDesignationRecord>> QueryAsync(
        string sql,
        NpgsqlParameter[] parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<ProjectLeaseReadyDesignationRecord>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new ProjectLeaseReadyDesignationRecord(
                    reader.GetString(0),
                    reader.GetGuid(1),
                    reader.GetString(2)));
            }

            return rows;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return [];
        }
    }
}
