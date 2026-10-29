using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class ComplaintQueueOrderTests
{
    [Fact]
    public void Queue_PostgresTranslatesDateAndIdKeysetWithoutConnecting()
    {
        using var db = new GovernanceIntelligenceDbContext(
            new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=translation_only").Options);
        var sql = ComplaintQueueOrder.Apply(db.Set<RegisteredComplaintEntity>(), Rows()[0]).Take(51).ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void Queue_OrdersByReceivedDateBeforeId()
    {
        var rows = Rows();
        Assert.Equal(rows.OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id).Select(x => x.Id),
            ComplaintQueueOrder.Apply(rows.AsQueryable()).Select(x => x.Id));
    }

    [Fact]
    public void Queue_LoadMorePreservesDateOrderWithoutDuplicatesOrMissingTies()
    {
        var rows = Rows();
        var expected = ComplaintQueueOrder.Apply(rows.AsQueryable()).ToArray();
        var actual = new List<RegisteredComplaintEntity>();
        RegisteredComplaintEntity? anchor = null;
        while (true)
        {
            var page = ComplaintQueueOrder.Apply(rows.AsQueryable(), anchor).Take(50).ToArray();
            if (page.Length == 0) break;
            actual.AddRange(page);
            anchor = page[^1];
        }
        Assert.Equal(expected.Select(x => x.Id), actual.Select(x => x.Id));
        Assert.Equal(125, actual.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Queue_PaginatesOnlyWithinFilteredQueue()
    {
        var rows = Rows();
        var filtered = rows.Where((_, i) => i % 2 == 0).AsQueryable();
        var first = ComplaintQueueOrder.Apply(filtered).Take(50).ToArray();
        var rest = ComplaintQueueOrder.Apply(filtered, first[^1]).ToArray();
        Assert.Equal(ComplaintQueueOrder.Apply(filtered).Select(x => x.Id), first.Concat(rest).Select(x => x.Id));
    }

    private static RegisteredComplaintEntity[] Rows() => Enumerable.Range(1, 125)
        .Select(i => new RegisteredComplaintEntity
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}"),
            SubmittedAtUtc = DateTimeOffset.Parse("2026-10-25T00:00:00Z").AddDays(i % 3)
        }).Reverse().ToArray();
}
