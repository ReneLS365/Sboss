using System.Collections.Concurrent;
using System.Text.Json;
using Sboss.Api.Validation;
using Sboss.Contracts.Commands;
using Sboss.Contracts.Economy;
using Sboss.Contracts.MatchResults;
using Sboss.Contracts.LevelSeeds;
using Sboss.Contracts.Seasons;
using Sboss.Contracts.ContractJobs;
using Sboss.Contracts.ContractJobApplications;
using Sboss.Domain.Entities;
using Sboss.Infrastructure;
using Sboss.Infrastructure.Repositories;
using Sboss.Infrastructure.Services;

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
    IAccountRepository accountRepository,
    ISeasonRepository seasonRepository,
    ILevelSeedRepository levelSeedRepository,
    IMatchResultRepository repository,
    ICommandValidationQueue commandValidationQueue,
    IScoringEngine scoringEngine,
    IMatchResultValidationService validator,
    ConcurrentDictionary<Guid, SemaphoreSlim> locks,
    CancellationToken cancellationToken) =>
{
    var account = await accountRepository.GetByIdAsync(request.AccountId, cancellationToken);
    var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
    var levelSeed = await levelSeedRepository.GetByIdAsync(request.LevelSeedId, cancellationToken);

    var referenceErrors = new Dictionary<string, string[]>();
    if (account is null)
    {
        referenceErrors["accountId"] = new[] { "Account does not exist." };
    }

    if (season is null)
    {
        referenceErrors["seasonId"] = new[] { "Season does not exist." };
    }

    if (levelSeed is null)
    {
        referenceErrors["levelSeedId"] = new[] { "Level seed does not exist." };
    }

    if (referenceErrors.Count > 0)
    {
        return Results.ValidationProblem(referenceErrors);
    }

    if (request.PlacementIntents is null || request.PlacementIntents.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["placementIntents"] = new[] { "At least one placement intent is required." }
        });
    }

    if (request.PlacementIntents.Any(intent => intent.SeedId != request.LevelSeedId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["placementIntents"] = new[] { "All placement intents must target the requested level seed." }
        });
    }

    var validationResults = new List<CommandValidationResult>(request.PlacementIntents.Count);
    foreach (var intent in request.PlacementIntents)
    {
        var rawIntentJson = JsonSerializer.Serialize(intent);
        var validation = await commandValidationQueue.ValidatePlaceComponentIntentAsync(rawIntentJson, cancellationToken);
        validationResults.Add(validation);
    }

    var scoring = scoringEngine.Compute(levelSeed!, validationResults.Select(result => result.Accepted).ToArray());

    MatchResult matchResult;

    try
    {
        matchResult = MatchResult.Create(
            request.AccountId,
            request.SeasonId,
            request.LevelSeedId,
            scoring.Score,
            scoring.ClearTimeMs,
            scoring.ComboMax,
            scoring.Penalties,
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
        return Results.Created(
            $"/api/v1/match-results/{saved.MatchResultId}",
            new PostMatchResultResponse(
                saved.MatchResultId,
                saved.Score,
                saved.ComboMax,
                scoring.StabilityPercent,
                saved.Penalties,
                saved.ValidationStatus.ToString().ToLowerInvariant(),
                saved.CreatedAt,
                saved.Version));
    }
    finally
    {
        gate.Release();
    }
});

app.MapPost("/api/v1/economy/transactions", async (
    PostEconomyTransactionRequest request,
    IEconomyTransactionService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.ApplyAsync(
            new EconomyMutationRequest(
                request.AccountId,
                request.CurrencyCode,
                request.AmountDelta,
                request.IdempotencyKey,
                request.Reason),
            cancellationToken);

        return Results.Ok(MapEconomyTransaction(result));
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.UnknownAccount)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.InsufficientFunds)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["economyTransaction"] = new[] { exception.Message }
        });
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/contract-jobs/{contractJobId:guid}/transitions", async (
    Guid contractJobId,
    PostContractJobTransitionRequest request,
    IContractJobTransitionService service,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<ContractJobState>(request.TargetState, ignoreCase: true, out var targetState))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["targetState"] = new[] { "Target state is invalid." }
        });
    }

    try
    {
        var result = await service.TransitionAsync(
            new ContractJobTransitionRequest(contractJobId, targetState, request.IdempotencyKey),
            cancellationToken);

        return Results.Ok(MapContractJobTransition(result));
    }
    catch (ContractJobTransitionServiceException exception) when (exception.Reason == ContractJobTransitionFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ContractJobTransitionServiceException exception) when (exception.Reason == ContractJobTransitionFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["contractJobTransition"] = new[] { exception.Message }
        });
    }
    catch (ContractJobTransitionServiceException exception) when (exception.Reason == ContractJobTransitionFailureReason.InvalidTransition)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/contract-jobs/{contractJobId:guid}/applications", async (
    Guid contractJobId,
    PostContractJobApplicationRequest request,
    IContractJobApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.SubmitApplicationAsync(
            new SubmitContractJobApplicationRequest(contractJobId, request.ApplicantAccountId, request.IdempotencyKey),
            cancellationToken);

        return Results.Ok(MapContractJobApplication(result));
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["contractJobApplication"] = new[] { exception.Message }
        });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/contract-jobs/{contractJobId:guid}/applications/{applicationId:guid}/withdraw", async (
    Guid contractJobId,
    Guid applicationId,
    PostContractJobApplicationMutationRequest request,
    IContractJobApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.WithdrawApplicationAsync(
            new WithdrawContractJobApplicationRequest(contractJobId, applicationId, request.IdempotencyKey),
            cancellationToken);

        return Results.Ok(MapContractJobApplication(result));
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["contractJobApplication"] = new[] { exception.Message }
        });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/contract-jobs/{contractJobId:guid}/applications/{applicationId:guid}/accept", async (
    Guid contractJobId,
    Guid applicationId,
    PostContractJobApplicationMutationRequest request,
    IContractJobApplicationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.AcceptApplicationAsync(
            new AcceptContractJobApplicationRequest(contractJobId, applicationId, request.IdempotencyKey),
            cancellationToken);

        return Results.Ok(MapContractJobApplication(result));
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["contractJobApplication"] = new[] { exception.Message }
        });
    }
    catch (ContractJobApplicationServiceException exception) when (exception.Reason == ContractJobApplicationFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.Run();

static CurrentSeasonResponse MapSeason(Season season) =>
    new(season.SeasonId, season.Name, season.StartsAt, season.EndsAt, season.IsActive, season.Version);

static LevelSeedResponse MapLevelSeed(LevelSeed seed) =>
    new(seed.LevelSeedId, seed.SeedValue, seed.Biome, seed.Template, seed.Objective, seed.ModifiersJson, seed.ParTimeMs, seed.GoldTimeMs, seed.Version);

static PostEconomyTransactionResponse MapEconomyTransaction(EconomyTransactionResult result) =>
    new(
        result.Transaction.EconomyTransactionId,
        result.Transaction.AccountId,
        result.Transaction.CurrencyCode,
        result.Transaction.AmountDelta,
        result.Balance.Balance,
        result.Transaction.Reason,
        result.IsIdempotentReplay ? "idempotent_replay" : "applied",
        result.Transaction.CreatedAt,
        result.Balance.Version,
        result.Transaction.Version);

static PostContractJobTransitionResponse MapContractJobTransition(ContractJobTransitionResult result) =>
    new(
        result.Job.ContractJobId,
        result.Job.OwningAccountId,
        result.Job.CurrentState.ToString(),
        result.IsIdempotentReplay ? "idempotent_replay" : "applied",
        result.Job.CreatedAt,
        result.Job.UpdatedAt,
        result.Job.Version);

static PostContractJobApplicationResponse MapContractJobApplication(ContractJobApplicationMutationResult result) =>
    new(
        result.Application.ContractJobApplicationId,
        result.Application.ContractJobId,
        result.Application.ApplicantAccountId,
        result.Application.Status.ToString(),
        result.IsIdempotentReplay ? "idempotent_replay" : "applied",
        result.Application.CreatedAt,
        result.Application.UpdatedAt,
        result.Application.Version,
        result.ResultingJobState?.ToString(),
        result.AcceptedApplicationId);

public partial class Program;
