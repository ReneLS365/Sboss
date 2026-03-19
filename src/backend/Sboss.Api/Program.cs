using System.Collections.Concurrent;
using Sboss.Api.Validation;
using Sboss.Contracts.MatchResults;
using Sboss.Contracts.LevelSeeds;
using Sboss.Contracts.Seasons;
using Sboss.Domain.Entities;
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
    return Results.Ok(MapSeason(season));
});

app.MapGet("/api/v1/level-seeds/{seedId:guid}", async (Guid seedId, ILevelSeedRepository repository, CancellationToken cancellationToken) =>
{
    var seed = await repository.GetByIdAsync(seedId, cancellationToken);
    return seed is null ? Results.NotFound() : Results.Ok(MapLevelSeed(seed));
});

app.MapPost("/api/v1/match-results", async (
    PostMatchResultRequest request,
    IMatchResultRepository repository,
    IMatchResultValidationService validator,
    ConcurrentDictionary<Guid, SemaphoreSlim> locks,
    CancellationToken cancellationToken) =>
{
    MatchResult matchResult;

    try
    {
        matchResult = MatchResult.Create(
            request.AccountId,
            request.SeasonId,
            request.LevelSeedId,
            request.Score,
            request.ClearTimeMs,
            request.ComboMax,
            request.Penalties,
            DateTimeOffset.UtcNow);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["matchResult"] = new[] { ex.Message }
        });
    }

    var gate = locks.GetOrAdd(matchResult.AccountId, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(cancellationToken);
    try
    {
        var validationStatus = await validator.ValidateAsync(matchResult, cancellationToken);
        matchResult.ApplyValidation(validationStatus, DateTimeOffset.UtcNow);
        var saved = await repository.SaveAsync(matchResult, cancellationToken);
        return Results.Created($"/api/v1/match-results/{saved.MatchResultId}", MapMatchResult(saved));
    }
    finally
    {
        gate.Release();
    }
});

app.Run();

static CurrentSeasonResponse MapSeason(Season season) =>
    new(season.SeasonId, season.Name, season.StartsAt, season.EndsAt, season.IsActive, season.Version);

static LevelSeedResponse MapLevelSeed(LevelSeed seed) =>
    new(seed.LevelSeedId, seed.SeedValue, seed.Biome, seed.Template, seed.Objective, seed.ModifiersJson, seed.ParTimeMs, seed.GoldTimeMs, seed.Version);

static PostMatchResultResponse MapMatchResult(MatchResult matchResult) =>
    new(matchResult.MatchResultId, matchResult.ValidationStatus.ToString().ToLowerInvariant(), matchResult.CreatedAt, matchResult.Version);

public partial class Program;
