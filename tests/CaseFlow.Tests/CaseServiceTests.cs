using CaseFlow.Api.Contracts;
using CaseFlow.Api.Data;
using CaseFlow.Api.Domain;
using CaseFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CaseFlow.Tests;

public sealed class CaseServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private CaseDbContext _db = null!;
    private CaseService _service = null!;
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CaseDbContext>().UseSqlite(_connection).Options;
        _db = new CaseDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _service = new CaseService(_db, _clock);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static CreateCaseRequest ValidRequest(CasePriority priority = CasePriority.Normal) => new(
        "Document export returns duplicate files",
        "The batch export job occasionally generates duplicate documents for one account.",
        "Northwind Retail", "ops@northwind.example", priority, "Alex");

    [Fact]
    public async Task CreatePersistsCaseAndActivity()
    {
        var created = await _service.CreateAsync(ValidRequest());
        Assert.True(created.Succeeded);
        Assert.StartsWith("CF-", created.Value!.Reference);
        Assert.Equal(CaseStatus.Open, created.Value.Status);
        Assert.Equal(1, created.Value.Version);
        Assert.Single(created.Value.Activities);

        var fetched = await _service.GetAsync(created.Value.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Northwind Retail", fetched.CustomerName);
    }

    [Fact]
    public async Task CreateRejectsInvalidCustomerEmail()
    {
        var bad = ValidRequest() with { CustomerEmail = "not-an-email" };
        var outcome = await _service.CreateAsync(bad);
        Assert.False(outcome.Succeeded);
        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal(0, await _db.Cases.CountAsync());
    }

    [Fact]
    public async Task IllegalStateTransitionReturns422()
    {
        var created = (await _service.CreateAsync(ValidRequest())).Value!;
        var outcome = await _service.ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(CaseStatus.Closed, created.Version, "Alex"));
        Assert.Equal(422, outcome.StatusCode);
        Assert.Equal(CaseStatus.Open, (await _service.GetAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task StaleVersionReturnsConflict()
    {
        var created = (await _service.CreateAsync(ValidRequest())).Value!;
        var changed = await _service.ChangeStatusAsync(created.Id,
            new ChangeStatusRequest(CaseStatus.InProgress, created.Version, "Alex"));
        Assert.True(changed.Succeeded);
        var stale = await _service.AssignAsync(created.Id,
            new AssignCaseRequest("Sam", created.Version, "Alex"));
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(2, changed.Value!.Version);
    }

    [Fact]
    public async Task NotesArePersistedWithAuditHistory()
    {
        var created = (await _service.CreateAsync(ValidRequest())).Value!;
        var updated = await _service.AddNoteAsync(created.Id,
            new AddNoteRequest("Alex", "Investigating the repeated batch event.", created.Version));
        Assert.True(updated.Succeeded);
        Assert.Single(updated.Value!.Notes);
        Assert.Equal(2, updated.Value.Activities.Count);
        Assert.Equal(2, updated.Value.Version);
        var fetched = await _service.GetAsync(created.Id);
        Assert.Single(fetched!.Notes);
    }

    [Fact]
    public async Task DashboardCountsOverdueActiveCases()
    {
        await _service.CreateAsync(ValidRequest(CasePriority.Critical));
        _clock.Advance(TimeSpan.FromHours(5));
        var summary = await _service.DashboardAsync();
        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.Overdue);
        Assert.Equal(1, summary.Critical);
    }

    [Fact]
    public async Task PaginationAndStatusFilterWork()
    {
        var first = (await _service.CreateAsync(ValidRequest())).Value!;
        await _service.CreateAsync(ValidRequest() with { Title = "Second case with a unique title" });
        await _service.ChangeStatusAsync(first.Id,
            new ChangeStatusRequest(CaseStatus.InProgress, 1, "Alex"));
        var page = await _service.ListAsync(1, 10, null, CaseStatus.InProgress, null);
        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(first.Id, page.Items[0].Id);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
