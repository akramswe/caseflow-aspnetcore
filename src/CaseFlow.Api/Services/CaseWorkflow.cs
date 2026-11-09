using CaseFlow.Api.Domain;

namespace CaseFlow.Api.Services;

/// <summary>Business rules kept separate from HTTP/database concerns for easy testing.</summary>
public static class CaseWorkflow
{
    public static bool CanTransition(CaseStatus from, CaseStatus to) => (from, to) switch
    {
        (CaseStatus.Open, CaseStatus.InProgress) => true,
        (CaseStatus.InProgress, CaseStatus.Open or CaseStatus.WaitingOnCustomer or CaseStatus.Resolved) => true,
        (CaseStatus.WaitingOnCustomer, CaseStatus.InProgress or CaseStatus.Resolved) => true,
        (CaseStatus.Resolved, CaseStatus.InProgress or CaseStatus.Closed) => true,
        (CaseStatus.Closed, CaseStatus.InProgress) => true,
        _ => false
    };

    public static DateTime CalculateDueAtUtc(DateTime createdAtUtc, CasePriority priority) =>
        createdAtUtc.AddHours(priority switch
        {
            CasePriority.Critical => 4,
            CasePriority.High => 24,
            CasePriority.Normal => 72,
            CasePriority.Low => 168,
            _ => throw new ArgumentOutOfRangeException(nameof(priority))
        });

    public static bool IsOverdue(SupportCase item, DateTime nowUtc) =>
        item.Status is not (CaseStatus.Resolved or CaseStatus.Closed)
        && item.DueAtUtc < nowUtc;
}
