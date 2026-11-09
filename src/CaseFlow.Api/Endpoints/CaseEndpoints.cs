using System.Text;
using CaseFlow.Api.Contracts;
using CaseFlow.Api.Domain;
using CaseFlow.Api.Services;

namespace CaseFlow.Api.Endpoints;

public static class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var cases = app.MapGroup("/api/cases").WithTags("Cases");

        cases.MapGet("", async (CaseService service, CancellationToken ct,
            int page = 1, int pageSize = 20, string? search = null,
            CaseStatus? status = null, CasePriority? priority = null) =>
        {
            if (page < 1 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { error = "Page must be >= 1 and pageSize must be 1–100." });
            return Results.Ok(await service.ListAsync(page, pageSize, search, status, priority, ct));
        });

        cases.MapGet("/export.csv", async (CaseService service, CancellationToken ct) =>
            Results.File(Encoding.UTF8.GetBytes(await service.ExportCsvAsync(ct)),
                "text/csv; charset=utf-8", "caseflow-cases.csv"));

        cases.MapGet("/{id:guid}", async (Guid id, CaseService service, CancellationToken ct) =>
        {
            var item = await service.GetAsync(id, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        cases.MapPost("", async (CreateCaseRequest request, CaseService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.Succeeded
                ? Results.Created($"/api/cases/{result.Value!.Id}", result.Value)
                : ToError(result.StatusCode, result.Error!);
        });

        cases.MapPut("/{id:guid}/status", async (Guid id, ChangeStatusRequest request,
            CaseService service, CancellationToken ct) =>
            ToResult(await service.ChangeStatusAsync(id, request, ct)));

        cases.MapPut("/{id:guid}/assignment", async (Guid id, AssignCaseRequest request,
            CaseService service, CancellationToken ct) =>
            ToResult(await service.AssignAsync(id, request, ct)));

        cases.MapPost("/{id:guid}/notes", async (Guid id, AddNoteRequest request,
            CaseService service, CancellationToken ct) =>
            ToResult(await service.AddNoteAsync(id, request, ct)));

        app.MapGet("/api/dashboard", async (CaseService service, CancellationToken ct) =>
            Results.Ok(await service.DashboardAsync(ct))).WithTags("Dashboard");

        return app;
    }

    private static IResult ToResult<T>(ServiceResult<T> result) => result.Succeeded
        ? Results.Ok(result.Value)
        : ToError(result.StatusCode, result.Error!);

    private static IResult ToError(int status, string detail) =>
        Results.Problem(statusCode: status, title: "Unable to complete request", detail: detail);
}
