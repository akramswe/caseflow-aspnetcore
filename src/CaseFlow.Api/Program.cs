using System.Text.Json.Serialization;
using CaseFlow.Api.Data;
using CaseFlow.Api.Endpoints;
using CaseFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CaseService>();
// Resolve SQLite relative to the project content root, not the current shell directory.
var sqlite = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Cases") ?? "Data Source=data/caseflow.db");
if (!Path.IsPathRooted(sqlite.DataSource))
    sqlite.DataSource = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqlite.DataSource));
Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
builder.Services.AddDbContext<CaseDbContext>(options => options.UseSqlite(sqlite.ToString()));

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

// This demo uses EnsureCreated (no migration dependency) and fictional sample data.
// Use EF migrations, authentication, authorization, and secrets management before production use.
using (var scope = app.Services.CreateScope())
{
    await DemoSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<CaseDbContext>(),
        scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

app.MapCaseEndpoints();
app.MapGet("/health", async (CaseDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "healthy", database = "connected" })
        : Results.Problem(statusCode: 503, title: "Database unavailable"));

app.Run();
