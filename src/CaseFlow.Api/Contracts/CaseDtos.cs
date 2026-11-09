using CaseFlow.Api.Domain;

namespace CaseFlow.Api.Contracts;

public sealed record CreateCaseRequest(
    string? Title, string? Description, string? CustomerName,
    string? CustomerEmail, CasePriority? Priority, string? AssignedTo);

public sealed record ChangeStatusRequest(CaseStatus? Status, int? Version, string? Actor);
public sealed record AssignCaseRequest(string? AssignedTo, int? Version, string? Actor);
public sealed record AddNoteRequest(string? Author, string? Message, int? Version);

public sealed record CaseListItem(
    Guid Id, string Reference, string Title, string CustomerName,
    string CustomerEmail, string? AssignedTo, CasePriority Priority,
    CaseStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    DateTime DueAtUtc, int Version);

public sealed record CaseNoteDto(Guid Id, string Author, string Message, DateTime CreatedAtUtc);
public sealed record CaseActivityDto(Guid Id, string Actor, string EventType, string Description, DateTime CreatedAtUtc);

public sealed record CaseDetail(
    Guid Id, string Reference, string Title, string Description,
    string CustomerName, string CustomerEmail, string? AssignedTo,
    CasePriority Priority, CaseStatus Status, DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc, DateTime DueAtUtc, DateTime? ResolvedAtUtc,
    int Version, IReadOnlyList<CaseNoteDto> Notes,
    IReadOnlyList<CaseActivityDto> Activities);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record DashboardSummary(
    int Total, int Open, int InProgress, int WaitingOnCustomer,
    int Resolved, int Closed, int Overdue, int Critical);

public sealed record ServiceResult<T>(T? Value, string? Error, int StatusCode)
{
    public bool Succeeded => Error is null;
    public static ServiceResult<T> Success(T value) => new(value, null, 200);
    public static ServiceResult<T> Failure(int statusCode, string error) => new(default, error, statusCode);
}
