using System.Text;
using CaseFlow.Api.Contracts;
using CaseFlow.Api.Data;
using CaseFlow.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Api.Services;

public sealed class CaseService(CaseDbContext db, TimeProvider clock)
{
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<PagedResponse<CaseListItem>> ListAsync(
        int page, int pageSize, string? search, CaseStatus? status,
        CasePriority? priority, CancellationToken ct = default)
    {
        IQueryable<SupportCase> query = db.Cases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.Like(x.Reference, pattern)
                || EF.Functions.Like(x.Title, pattern)
                || EF.Functions.Like(x.CustomerName, pattern));
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (priority.HasValue) query = query.Where(x => x.Priority == priority.Value);

        var total = await query.CountAsync(ct);
        var cases = await query.OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Reference)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<CaseListItem>(cases.Select(ToListItem).ToList(), page, pageSize, total);
    }

    public async Task<CaseDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.Cases.AsNoTracking()
            .Include(x => x.Notes).Include(x => x.Activities).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : ToDetail(item);
    }

    public async Task<ServiceResult<CaseDetail>> CreateAsync(CreateCaseRequest request, CancellationToken ct = default)
    {
        var error = CaseValidation.ValidateCreate(request);
        if (error is not null) return ServiceResult<CaseDetail>.Failure(400, error);

        var now = UtcNow;
        var priority = request.Priority!.Value;
        var item = new SupportCase
        {
            Reference = ($"CF-{now:yyyyMMdd}-{Guid.NewGuid():N}")[..20].ToUpperInvariant(),
            Title = request.Title!.Trim(),
            Description = request.Description!.Trim(),
            CustomerName = request.CustomerName!.Trim(),
            CustomerEmail = request.CustomerEmail!.Trim(),
            Priority = priority,
            AssignedTo = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim(),
            Status = CaseStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            DueAtUtc = CaseWorkflow.CalculateDueAtUtc(now, priority)
        };
        item.Activities.Add(new CaseActivity
        {
            CaseId = item.Id, Actor = "System", EventType = "created",
            Description = "Case created", CreatedAtUtc = now
        });
        db.Cases.Add(item);
        await db.SaveChangesAsync(ct);
        return ServiceResult<CaseDetail>.Success(ToDetail(item));
    }

    public async Task<ServiceResult<CaseDetail>> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct = default)
    {
        if (request.Status is null || !Enum.IsDefined(request.Status.Value)
            || request.Version is null || request.Version.Value <= 0
            || !CaseValidation.ValidActor(request.Actor))
            return ServiceResult<CaseDetail>.Failure(400, "Valid status, version and actor are required.");

        var item = await LoadForUpdateAsync(id, ct);
        if (item is null) return ServiceResult<CaseDetail>.Failure(404, "Case not found.");
        if (item.Version != request.Version.Value) return VersionConflict();
        if (!CaseWorkflow.CanTransition(item.Status, request.Status.Value))
            return ServiceResult<CaseDetail>.Failure(422,
                $"Transition from {item.Status} to {request.Status.Value} is not permitted.");

        var previous = item.Status;
        item.Status = request.Status.Value;
        item.UpdatedAtUtc = UtcNow;
        item.Version++;
        if (item.Status is CaseStatus.Resolved or CaseStatus.Closed)
            item.ResolvedAtUtc ??= item.UpdatedAtUtc;
        else
            item.ResolvedAtUtc = null;
        item.Activities.Add(new CaseActivity
        {
            CaseId = id, Actor = CaseValidation.ActorOrDefault(request.Actor),
            EventType = "status_changed", Description = $"{previous} → {item.Status}",
            CreatedAtUtc = item.UpdatedAtUtc
        });
        return await PersistAsync(item, ct);
    }

    public async Task<ServiceResult<CaseDetail>> AssignAsync(Guid id, AssignCaseRequest request, CancellationToken ct = default)
    {
        if (request.Version is null || request.Version.Value <= 0
            || request.AssignedTo is not null && request.AssignedTo.Trim().Length > 80
            || !CaseValidation.ValidActor(request.Actor))
            return ServiceResult<CaseDetail>.Failure(400, "Valid version and assignee (max 80 characters) are required.");

        var item = await LoadForUpdateAsync(id, ct);
        if (item is null) return ServiceResult<CaseDetail>.Failure(404, "Case not found.");
        if (item.Version != request.Version.Value) return VersionConflict();
        var assignee = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim();
        if (item.AssignedTo == assignee)
            return ServiceResult<CaseDetail>.Failure(422, "Assignment has not changed.");
        item.AssignedTo = assignee;
        item.UpdatedAtUtc = UtcNow;
        item.Version++;
        item.Activities.Add(new CaseActivity
        {
            CaseId = id, Actor = CaseValidation.ActorOrDefault(request.Actor),
            EventType = "assigned", Description = assignee is null ? "Assignment cleared" : $"Assigned to {assignee}",
            CreatedAtUtc = item.UpdatedAtUtc
        });
        return await PersistAsync(item, ct);
    }

    public async Task<ServiceResult<CaseDetail>> AddNoteAsync(Guid id, AddNoteRequest request, CancellationToken ct = default)
    {
        if (!CaseValidation.Between(request.Author, 2, 80)
            || !CaseValidation.Between(request.Message, 2, 1000)
            || request.Version is null || request.Version.Value <= 0)
            return ServiceResult<CaseDetail>.Failure(400, "Author (2–80), message (2–1000) and version are required.");

        var item = await LoadForUpdateAsync(id, ct);
        if (item is null) return ServiceResult<CaseDetail>.Failure(404, "Case not found.");
        if (item.Version != request.Version.Value) return VersionConflict();
        item.UpdatedAtUtc = UtcNow;
        item.Version++;
        item.Notes.Add(new CaseNote
        {
            CaseId = id, Author = request.Author!.Trim(),
            Message = request.Message!.Trim(), CreatedAtUtc = item.UpdatedAtUtc
        });
        item.Activities.Add(new CaseActivity
        {
            CaseId = id, Actor = request.Author!.Trim(), EventType = "note_added",
            Description = "Internal note added", CreatedAtUtc = item.UpdatedAtUtc
        });
        return await PersistAsync(item, ct);
    }

    public async Task<DashboardSummary> DashboardAsync(CancellationToken ct = default)
    {
        var now = UtcNow;
        var cases = db.Cases.AsNoTracking();
        return new DashboardSummary(
            await cases.CountAsync(ct),
            await cases.CountAsync(x => x.Status == CaseStatus.Open, ct),
            await cases.CountAsync(x => x.Status == CaseStatus.InProgress, ct),
            await cases.CountAsync(x => x.Status == CaseStatus.WaitingOnCustomer, ct),
            await cases.CountAsync(x => x.Status == CaseStatus.Resolved, ct),
            await cases.CountAsync(x => x.Status == CaseStatus.Closed, ct),
            await cases.CountAsync(x => x.DueAtUtc < now && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed, ct),
            await cases.CountAsync(x => x.Priority == CasePriority.Critical && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed, ct)
        );
    }

    public async Task<string> ExportCsvAsync(CancellationToken ct = default)
    {
        var cases = await db.Cases.AsNoTracking().OrderBy(x => x.Reference).ToListAsync(ct);
        var csv = new StringBuilder("Reference,Title,Customer,Email,Priority,Status,AssignedTo,CreatedAtUtc,DueAtUtc\r\n");
        foreach (var c in cases)
        {
            csv.AppendJoin(',', new[]
            {
                CsvCell(c.Reference), CsvCell(c.Title), CsvCell(c.CustomerName),
                CsvCell(c.CustomerEmail), CsvCell(c.Priority.ToString()), CsvCell(c.Status.ToString()),
                CsvCell(c.AssignedTo ?? ""), CsvCell(c.CreatedAtUtc.ToString("O")), CsvCell(c.DueAtUtc.ToString("O"))
            }).Append("\r\n");
        }
        return csv.ToString();
    }

    // Escape quotes and defend against CSV spreadsheet-formula injection.
    public static string CsvCell(string text)
    {
        var lead = text.TrimStart();
        if (lead.Length > 0 && "=+-@".Contains(lead[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private Task<SupportCase?> LoadForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Cases.Include(x => x.Notes).Include(x => x.Activities).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task<ServiceResult<CaseDetail>> PersistAsync(SupportCase item, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult<CaseDetail>.Success(ToDetail(item));
        }
        catch (DbUpdateConcurrencyException)
        {
            return VersionConflict();
        }
    }

    private static ServiceResult<CaseDetail> VersionConflict() =>
        ServiceResult<CaseDetail>.Failure(409, "This case changed since it was loaded. Refresh and try again.");

    private static CaseListItem ToListItem(SupportCase c) => new(
        c.Id, c.Reference, c.Title, c.CustomerName, c.CustomerEmail,
        c.AssignedTo, c.Priority, c.Status, c.CreatedAtUtc,
        c.UpdatedAtUtc, c.DueAtUtc, c.Version);

    private static CaseDetail ToDetail(SupportCase c) => new(
        c.Id, c.Reference, c.Title, c.Description, c.CustomerName,
        c.CustomerEmail, c.AssignedTo, c.Priority, c.Status,
        c.CreatedAtUtc, c.UpdatedAtUtc, c.DueAtUtc, c.ResolvedAtUtc,
        c.Version,
        c.Notes.OrderByDescending(n => n.CreatedAtUtc).Select(n =>
            new CaseNoteDto(n.Id, n.Author, n.Message, n.CreatedAtUtc)).ToList(),
        c.Activities.OrderByDescending(a => a.CreatedAtUtc).Select(a =>
            new CaseActivityDto(a.Id, a.Actor, a.EventType, a.Description, a.CreatedAtUtc)).ToList());
}
