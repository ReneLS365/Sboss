using System.Collections.Concurrent;
using Sboss.Api.Validation;
using Sboss.Contracts.MatchResults;
using Sboss.Infrastructure;
using Sboss.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSbossInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IMatchResultValidationService, MatchResultValidationService>();
builder.Services.AddSingleton(new ConcurrentDictionary<Guid, SemaphoreSlim>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", utcTime = DateTimeOffset.UtcNow }));

app.MapGet("/api/v1/seasons/current", async (ISeasonRepository repository, CancellationToken cancellationToken) =>
{
    var season = await repository.GetCurrentSeasonAsync(cancellationToken);
    return Results.Ok(season);
});

app.MapGet("/api/v1/level-seeds/{seedId:guid}", async (Guid seedId, ILevelSeedRepository repository, CancellationToken cancellationToken) =>
{
    var seed = await repository.GetByIdAsync(seedId, cancellationToken);
    return seed is null ? Results.NotFound() : Results.Ok(seed);
});

app.MapPost("/api/v1/match-results", async (
    PostMatchResultRequest request,
    IMatchResultRepository repository,
    IMatchResultValidationService validator,
    ConcurrentDictionary<Guid, SemaphoreSlim> locks,
    CancellationToken cancellationToken) =>
{
    var gate = locks.GetOrAdd(request.AccountId, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(cancellationToken);
    try
    {
        var validationStatus = await validator.ValidateAsync(request, cancellationToken);
        var saved = await repository.SaveAsync(request, validationStatus, cancellationToken);
        return Results.Created($"/api/v1/match-results/{saved.MatchResultId}", saved);
    }
    finally
    {
        gate.Release();
    }
});

app.Run();

public partial class Program;
