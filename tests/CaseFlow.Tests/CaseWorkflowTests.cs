using CaseFlow.Api.Domain;
using CaseFlow.Api.Services;
using Xunit;

namespace CaseFlow.Tests;

public sealed class CaseWorkflowTests
{
    [Theory]
    [InlineData(CaseStatus.Open, CaseStatus.InProgress, true)]
    [InlineData(CaseStatus.Open, CaseStatus.Closed, false)]
    [InlineData(CaseStatus.InProgress, CaseStatus.WaitingOnCustomer, true)]
    [InlineData(CaseStatus.InProgress, CaseStatus.Resolved, true)]
    [InlineData(CaseStatus.Resolved, CaseStatus.Closed, true)]
    [InlineData(CaseStatus.Closed, CaseStatus.InProgress, true)]
    [InlineData(CaseStatus.Closed, CaseStatus.Open, false)]
    [InlineData(CaseStatus.Open, CaseStatus.Open, false)]
    public void TransitionRulesAreExplicit(CaseStatus from, CaseStatus to, bool allowed)
    {
        Assert.Equal(allowed, CaseWorkflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(CasePriority.Critical, 4)]
    [InlineData(CasePriority.High, 24)]
    [InlineData(CasePriority.Normal, 72)]
    [InlineData(CasePriority.Low, 168)]
    public void DueDateDependsOnPriority(CasePriority priority, int hours)
    {
        var created = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(created.AddHours(hours), CaseWorkflow.CalculateDueAtUtc(created, priority));
    }

    [Fact]
    public void ResolvedCasesDoNotBecomeOverdue()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var item = new SupportCase { Status = CaseStatus.Resolved, DueAtUtc = now.AddHours(-5) };
        Assert.False(CaseWorkflow.IsOverdue(item, now));
        item.Status = CaseStatus.Open;
        Assert.True(CaseWorkflow.IsOverdue(item, now));
    }

    [Theory]
    [InlineData("=2+3")]
    [InlineData("+cmd")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1:A2)")]
    public void CsvEscapesFormulaLikeCells(string unsafeInput)
    {
        Assert.StartsWith("\"'", CaseService.CsvCell(unsafeInput));
    }
}
