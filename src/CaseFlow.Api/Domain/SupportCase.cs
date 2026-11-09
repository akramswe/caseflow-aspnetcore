namespace CaseFlow.Api.Domain;

public enum CasePriority { Low, Normal, High, Critical }
public enum CaseStatus { Open, InProgress, WaitingOnCustomer, Resolved, Closed }

public sealed class SupportCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public CasePriority Priority { get; set; } = CasePriority.Normal;
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    // Optimistic concurrency: compare this value on every write.
    public int Version { get; set; } = 1;
    public List<CaseNote> Notes { get; set; } = [];
    public List<CaseActivity> Activities { get; set; } = [];
}

public sealed class CaseNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CaseActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
