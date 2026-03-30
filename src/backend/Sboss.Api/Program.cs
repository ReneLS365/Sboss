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
using Sboss.Contracts.Yard;
using Sboss.Contracts.Crews;
using Sboss.Contracts.Loadout;
using Sboss.Contracts.FogOfWar;
using Sboss.Contracts.Progression;
using Sboss.Domain.Entities;
using Sboss.Infrastructure;
using Sboss.Infrastructure.Repositories;
using Sboss.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
const long WearPerInvalidSequenceEventBps = 2_500;

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

app.MapGet("/api/v1/progression/{accountId:guid}", async (
    Guid accountId,
    IProgressionService progressionService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var state = await progressionService.GetStateAsync(accountId, cancellationToken);
        return Results.Ok(new GetProgressionStateResponse(
            state.AccountId,
            state.TotalXp,
            state.Level,
            progressionService.GetNextLevelXpRequired(state.TotalXp),
            progressionService.GetUnlockedTemplates(state.Level)));
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["progression"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/progression/awards", async (
    PostProgressionAwardRequest request,
    IProgressionService progressionService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await progressionService.AwardFromMatchResultAsync(request.AccountId, request.MatchResultId, cancellationToken);
        return Results.Ok(new PostProgressionAwardResponse(
            result.AccountId,
            result.MatchResultId,
            result.XpAwarded,
            result.TotalXp,
            result.Level,
            result.LeveledUp,
            result.IsIdempotentReplay ? "idempotent_replay" : "awarded"));
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (InvalidOperationException exception) when (
        exception.Message.Contains("does not belong", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("accepted match results", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["progression"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/match-results", async (
    PostMatchResultRequest request,
    IAccountRepository accountRepository,
    ISeasonRepository seasonRepository,
    ILevelSeedRepository levelSeedRepository,
    IMatchResultRepository repository,
    ICommandValidationQueue commandValidationQueue,
    IAuthoritativeYardCapacityProvider yardCapacityProvider,
    IAuthoritativeComponentCapacityProvider componentCapacityProvider,
    IYardRepository yardRepository,
    ILoadoutRepository loadoutRepository,
    IAuthoritativeComponentCatalog componentCatalog,
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

    if (request.PlacementIntents.Any(intent => intent is null))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["placementIntents"] = new[] { "Placement intents cannot contain null entries." }
        });
    }

    if (request.PlacementIntents.Any(intent => intent.SeedId != request.LevelSeedId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["placementIntents"] = new[] { "All placement intents must target the requested level seed." }
        });
    }

    var supportedComponents = componentCatalog.GetSupportedComponents();
    var snapshot = await yardRepository.GetSnapshotAsync(request.AccountId, supportedComponents, cancellationToken);
    if (snapshot is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["accountId"] = new[] { "Account does not exist." }
        });
    }

    var remainingCapacityForSequence = await yardCapacityProvider.GetRemainingCapacityAsync(request.LevelSeedId, cancellationToken);
    var inventoryRemainingByItem = snapshot.InventoryByItemCode.ToDictionary(
        entry => entry.Key,
        entry => entry.Value.UsableQuantity,
        StringComparer.OrdinalIgnoreCase);
    var approvedLoadout = await loadoutRepository.GetAsync(request.AccountId, request.LevelSeedId, cancellationToken);
    var loadoutRemainingByItem = approvedLoadout?.QuantitiesByItemCode.ToDictionary(
        entry => entry.Key,
        entry => entry.Value,
        StringComparer.OrdinalIgnoreCase);
    var wearByItemCode = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    var validationResults = new List<CommandValidationResult>(request.PlacementIntents.Count);
    var acceptedComponentIdsInSequence = new List<string>(request.PlacementIntents.Count);
    foreach (var intent in request.PlacementIntents)
    {
        var rawIntentJson = JsonSerializer.Serialize(intent);
        var validation = await commandValidationQueue.ValidatePlaceComponentIntentAsync(
            rawIntentJson,
            acceptedComponentIdsInSequence,
            cancellationToken);

        if (validation.Accepted)
        {
            if (remainingCapacityForSequence.HasValue)
            {
                var requiredCapacity = await componentCapacityProvider.GetRequiredCapacityAsync(intent.ComponentId, cancellationToken);
                if (!requiredCapacity.HasValue || requiredCapacity.Value > remainingCapacityForSequence.Value)
                {
                    validation = CommandValidationResult.Reject(
                        "yard_capacity_exceeded",
                        "Required capacity exceeds remaining yard capacity for this placement sequence.");
                }
                else
                {
                    remainingCapacityForSequence -= requiredCapacity.Value;
                }
            }
        }

        if (validation.Accepted)
        {
            if (!componentCatalog.TryGetComponent(intent.ComponentId, out var component))
            {
                validation = CommandValidationResult.Reject(
                    "unknown_component_id",
                    $"ComponentId '{intent.ComponentId}' does not exist.");
            }
            else
            {
                var currentlyOwned = inventoryRemainingByItem.TryGetValue(component.ItemCode, out var quantity) ? quantity : 0;
                if (currentlyOwned <= 0)
                {
                    validation = CommandValidationResult.Reject(
                        "inventory_insufficient",
                        $"Component '{component.ItemCode}' is not available in owned inventory.");
                }

                if (validation.Accepted && loadoutRemainingByItem is not null)
                {
                    var currentlyLoaded = loadoutRemainingByItem.TryGetValue(component.ItemCode, out var loaded) ? loaded : 0;
                    if (currentlyLoaded <= 0)
                    {
                        validation = CommandValidationResult.Reject(
                            "loadout_missing_component",
                            $"Component '{component.ItemCode}' is not available in the approved loadout.");
                    }
                    else
                    {
                        loadoutRemainingByItem[component.ItemCode] = currentlyLoaded - 1;
                    }
                }

                if (validation.Accepted)
                {
                    inventoryRemainingByItem[component.ItemCode] = currentlyOwned - 1;
                }
            }
        }

        if (validation.Accepted)
        {
            acceptedComponentIdsInSequence.Add(intent.ComponentId);
        }

        validationResults.Add(validation);

        if (!validation.Accepted &&
            string.Equals(validation.Code, "scaffold_assembly_invalid_sequence", StringComparison.Ordinal) &&
            componentCatalog.TryGetComponent(intent.ComponentId, out var knownComponent) &&
            snapshot.InventoryByItemCode.TryGetValue(knownComponent.ItemCode, out var inventoryState) &&
            inventoryState.OwnedQuantity > 0)
        {
            wearByItemCode[knownComponent.ItemCode] = wearByItemCode.TryGetValue(knownComponent.ItemCode, out var totalWearBps)
                ? totalWearBps + WearPerInvalidSequenceEventBps
                : WearPerInvalidSequenceEventBps;
        }
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
        await yardRepository.ApplyWearAsync(request.AccountId, wearByItemCode, cancellationToken);
        var saved = await repository.SaveAsync(matchResult, cancellationToken);
        return Results.Created(
            $"/api/v1/match-results/{saved.MatchResultId}",
            new PostMatchResultResponse(
                saved.MatchResultId,
                saved.Score,
                saved.ComboMax,
                scoring.StabilityPercent,
                saved.Penalties,
                validationResults,
                saved.ValidationStatus.ToString().ToLowerInvariant(),
                saved.CreatedAt,
                saved.Version));
    }
    finally
    {
        gate.Release();
    }
});

app.MapGet("/api/v1/yard/{accountId:guid}", async (
    Guid accountId,
    IYardRepository yardRepository,
    IAuthoritativeComponentCatalog componentCatalog,
    CancellationToken cancellationToken) =>
{
    var supportedComponents = componentCatalog.GetSupportedComponents();
    var snapshot = await yardRepository.GetSnapshotAsync(accountId, supportedComponents, cancellationToken);
    if (snapshot is null)
    {
        return Results.NotFound(new { error = "Account does not exist." });
    }

    var inventory = supportedComponents
        .OrderBy(component => component.ItemCode, StringComparer.OrdinalIgnoreCase)
        .Select(component =>
        {
            var inventoryState = snapshot.InventoryByItemCode.TryGetValue(component.ItemCode, out var item)
                ? item
                : new YardInventoryState(0, 0, 0, 0);

            return new YardInventoryItemResponse(
                component.ItemCode,
                inventoryState.OwnedQuantity,
                inventoryState.OwnedQuantity,
                inventoryState.UsableQuantity,
                inventoryState.DamagedQuantity,
                inventoryState.TotalIntegrityBps,
                component.UnitCapacity,
                component.PurchaseCost);
        })
        .ToArray();

    return Results.Ok(new GetYardStateResponse(
        snapshot.AccountId,
        snapshot.MaxCapacity,
        snapshot.UsedCapacity,
        snapshot.RemainingCapacity,
        snapshot.CoinBalance,
        inventory));
});

app.MapPost("/api/v1/yard/{accountId:guid}/purchases", async (
    Guid accountId,
    PostYardPurchaseRequest request,
    IYardRepository yardRepository,
    IAuthoritativeComponentCatalog componentCatalog,
    CancellationToken cancellationToken) =>
{
    if (request.Quantity <= 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["quantity"] = new[] { "Quantity must be greater than zero." }
        });
    }

    if (string.IsNullOrWhiteSpace(request.ItemCode))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["itemCode"] = new[] { "ItemCode is required." }
        });
    }

    if (!componentCatalog.TryGetComponent(request.ItemCode, out var component))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["itemCode"] = new[] { $"Unknown itemCode '{request.ItemCode}'." }
        });
    }

    var maxSafeCapacityQuantity = int.MaxValue / component.UnitCapacity;
    if (request.Quantity > maxSafeCapacityQuantity)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["quantity"] = new[] { "Quantity is too large for safe capacity calculation." }
        });
    }

    var supportedComponents = componentCatalog.GetSupportedComponents();
    var result = await yardRepository.PurchaseAsync(accountId, component, request.Quantity, supportedComponents, cancellationToken);
    if (!result.Success)
    {
        return result.FailureCode switch
        {
            "missing_account" => Results.NotFound(new { error = result.FailureMessage }),
            "insufficient_funds" => Results.BadRequest(new { error = result.FailureMessage }),
            "capacity_overflow" => Results.BadRequest(new { error = result.FailureMessage }),
            _ => Results.BadRequest(new { error = result.FailureMessage ?? "Purchase failed." })
        };
    }

    var snapshot = result.Snapshot!;
    return Results.Ok(new PostYardPurchaseResponse(
        snapshot.AccountId,
        component.ItemCode,
        request.Quantity,
        component.PurchaseCost,
        component.PurchaseCost * request.Quantity,
        snapshot.CoinBalance,
        snapshot.MaxCapacity,
        snapshot.UsedCapacity,
        snapshot.RemainingCapacity,
        result.OwnedQuantity));
});

app.MapGet("/api/v1/loadout/{accountId:guid}/{levelSeedId:guid}", async (
    Guid accountId,
    Guid levelSeedId,
    ILoadoutRepository loadoutRepository,
    IAuthoritativeLoadoutRequirementProvider requirementProvider,
    CancellationToken cancellationToken) =>
{
    var snapshot = await loadoutRepository.GetAsync(accountId, levelSeedId, cancellationToken);
    if (snapshot is null)
    {
        return Results.Ok(new GetLoadoutStateResponse(
            accountId,
            levelSeedId,
            0,
            0,
            false,
            Array.Empty<LoadoutItemResponse>(),
            requirementProvider.GetRequiredItemQuantities(levelSeedId).Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    var missing = GetMissingRequiredComponents(snapshot.QuantitiesByItemCode, requirementProvider.GetRequiredItemQuantities(levelSeedId));
    return Results.Ok(new GetLoadoutStateResponse(
        snapshot.AccountId,
        snapshot.LevelSeedId,
        snapshot.MaxCapacity,
        snapshot.UsedCapacity,
        snapshot.IsComplete,
        snapshot.QuantitiesByItemCode
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new LoadoutItemResponse(pair.Key, pair.Value))
            .ToArray(),
        missing));
});

app.MapPost("/api/v1/loadout/{accountId:guid}/{levelSeedId:guid}", async (
    Guid accountId,
    Guid levelSeedId,
    PostLoadoutSubmissionRequest request,
    ILoadoutRepository loadoutRepository,
    ILevelSeedRepository levelSeedRepository,
    IAccountRepository accountRepository,
    IProgressionService progressionService,
    IAuthoritativeComponentCatalog componentCatalog,
    IAuthoritativeLoadoutRequirementProvider requirementProvider,
    CancellationToken cancellationToken) =>
{
    var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
    var levelSeed = await levelSeedRepository.GetByIdAsync(levelSeedId, cancellationToken);
    if (account is null || levelSeed is null)
    {
        return Results.NotFound(new { error = "Account or level seed does not exist." });
    }

    var progression = await progressionService.GetStateAsync(accountId, cancellationToken);
    if (!progressionService.IsTemplateUnlocked(levelSeed.Template, progression.Level))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = new[] { "At least one loadout item is required." } });
    }

    const int maxCapacity = 10;
    var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var usedCapacity = 0;
    foreach (var item in request.Items)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.ItemCode))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = new[] { "Each loadout item requires itemCode." } });
        }

        if (!componentCatalog.TryGetComponent(item.ItemCode, out var component))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = new[] { $"Unknown itemCode '{item.ItemCode}'." } });
        }

        if (item.Quantity < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["items"] = new[] { $"Quantity must be non-negative for '{item.ItemCode}'." } });
        }

        var maxSafeCapacityQuantity = int.MaxValue / component.UnitCapacity;
        if (item.Quantity > maxSafeCapacityQuantity)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = new[] { $"Quantity is too large for safe capacity calculation for '{item.ItemCode}'." }
            });
        }

        if (quantities.TryGetValue(component.ItemCode, out var existing))
        {
            if (existing > int.MaxValue - item.Quantity)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = new[] { $"Total quantity is too large for '{item.ItemCode}'." }
                });
            }

            quantities[component.ItemCode] = existing + item.Quantity;
        }
        else
        {
            quantities[component.ItemCode] = item.Quantity;
        }
    }

    foreach (var entry in quantities)
    {
        if (componentCatalog.TryGetComponent(entry.Key, out var component))
        {
            var maxSafeCapacityQuantity = int.MaxValue / component.UnitCapacity;
            if (entry.Value > maxSafeCapacityQuantity)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = new[] { $"Quantity is too large for safe capacity calculation for '{entry.Key}'." }
                });
            }

            usedCapacity = checked(usedCapacity + (component.UnitCapacity * entry.Value));
        }
    }

    if (usedCapacity > maxCapacity)
    {
        return Results.BadRequest(new { error = "loadout_capacity_exceeded", maxCapacity, usedCapacity });
    }

    var required = requirementProvider.GetRequiredItemQuantities(levelSeedId);
    var missingRequired = GetMissingRequiredComponents(quantities, required);
    var isComplete = missingRequired.Count == 0;
    var persisted = await loadoutRepository.UpsertAsync(
        new LoadoutSnapshot(accountId, levelSeedId, maxCapacity, usedCapacity, isComplete, quantities),
        cancellationToken);

    return Results.Ok(new PostLoadoutSubmissionResponse(
        persisted.AccountId,
        persisted.LevelSeedId,
        persisted.MaxCapacity,
        persisted.UsedCapacity,
        persisted.IsComplete,
        isComplete ? "accepted" : "incomplete",
        missingRequired,
        persisted.QuantitiesByItemCode
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new LoadoutItemResponse(pair.Key, pair.Value))
            .ToArray()));
});

app.MapGet("/api/v1/fog/{accountId:guid}/{levelSeedId:guid}", async (
    Guid accountId,
    Guid levelSeedId,
    IFogOfWarRepository fogRepository,
    CancellationToken cancellationToken) =>
{
    var revealed = await fogRepository.GetRevealedKeysAsync(accountId, levelSeedId, cancellationToken);
    return Results.Ok(new GetFogStateResponse(accountId, levelSeedId, revealed.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray()));
});

app.MapPost("/api/v1/fog/{accountId:guid}/{levelSeedId:guid}/reveal", async (
    Guid accountId,
    Guid levelSeedId,
    PostFogRevealRequest request,
    IAccountRepository accountRepository,
    ILevelSeedRepository levelSeedRepository,
    IFogOfWarRepository fogRepository,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.RevealKey))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["revealKey"] = new[] { "RevealKey is required." } });
    }

    var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
    var levelSeed = await levelSeedRepository.GetByIdAsync(levelSeedId, cancellationToken);
    if (account is null || levelSeed is null)
    {
        return Results.NotFound(new { error = "Account or level seed does not exist." });
    }

    var normalizedRevealKey = request.RevealKey.Trim().ToLowerInvariant();
    var inserted = await fogRepository.RevealAsync(accountId, levelSeedId, normalizedRevealKey, DateTimeOffset.UtcNow, cancellationToken);
    var revealed = await fogRepository.GetRevealedKeysAsync(accountId, levelSeedId, cancellationToken);
    return Results.Ok(new PostFogRevealResponse(
        accountId,
        levelSeedId,
        normalizedRevealKey,
        inserted ? "revealed" : "already_revealed",
        revealed.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray()));
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

app.MapPost("/api/v1/crews", async (
    PostCreateCrewRequest request,
    ICrewService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var crew = await service.CreateCrewAsync(request.OwnerAccountId, request.Name, cancellationToken);
        return Results.Ok(new PostCreateCrewResponse(crew.CrewId, crew.OwnerAccountId, crew.Name, crew.CreatedAt, crew.Version));
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crew"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/crews/{crewId:guid}/members", async (
    Guid crewId,
    PostCrewMemberAssignmentRequest request,
    ICrewService service,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<CrewRole>(request.Role, ignoreCase: true, out var role))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["role"] = new[] { "Crew role is invalid." }
        });
    }

    try
    {
        var member = await service.AssignMemberAsync(crewId, request.ActorAccountId, request.MemberAccountId, role, cancellationToken);
        return Results.Ok(new PostCrewMemberAssignmentResponse(member.CrewId, member.AccountId, member.Role.ToString(), member.UpdatedAt, member.Version));
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.Forbidden)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crewMember"] = new[] { exception.Message }
        });
    }
});

app.MapPost("/api/v1/crews/{crewId:guid}/members/{memberAccountId:guid}/remove", async (
    Guid crewId,
    Guid memberAccountId,
    PostCrewMemberRemovalRequest request,
    ICrewService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        await service.RemoveMemberAsync(crewId, request.ActorAccountId, memberAccountId, cancellationToken);
        return Results.NoContent();
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.Forbidden)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crewMember"] = new[] { exception.Message }
        });
    }
});

app.MapGet("/api/v1/crews/{crewId:guid}/split-preview", async (
    Guid crewId,
    long grossAmount,
    ICrewService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var split = await service.PreviewSplitAsync(crewId, grossAmount, cancellationToken);
        return Results.Ok(MapCrewSplitPreview(split));
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crewSplit"] = new[] { exception.Message }
        });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapPost("/api/v1/crews/{crewId:guid}/payouts", async (
    Guid crewId,
    PostCrewPayoutRequest request,
    ICrewService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var split = await service.SettlePayoutAsync(
            crewId,
            request.ActorAccountId,
            request.GrossAmount,
            request.CurrencyCode,
            request.IdempotencyKey,
            request.Reason,
            cancellationToken);

        return Results.Ok(new PostCrewPayoutResponse(
            split.CrewId,
            request.CurrencyCode.Trim().ToUpperInvariant(),
            split.GrossAmount,
            split.CompanyShareAmount,
            split.CrewShareAmount,
            split.Members.Select(MapCrewMemberBreakdown).ToArray()));
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.Forbidden)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crewPayout"] = new[] { exception.Message }
        });
    }
    catch (CrewServiceException exception) when (exception.Reason == CrewServiceFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.Conflict)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (EconomyTransactionServiceException exception) when (exception.Reason == EconomyTransactionFailureReason.InvalidRequest)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["crewPayout"] = new[] { exception.Message }
        });
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

static GetCrewSplitPreviewResponse MapCrewSplitPreview(CrewSplitResult result) =>
    new(
        result.CrewId,
        result.GrossAmount,
        result.CrewShareRatioBps,
        result.CrewShareAmount,
        result.CompanyShareAmount,
        result.Members.Select(MapCrewMemberBreakdown).ToArray());

static CrewMemberBreakdownResponse MapCrewMemberBreakdown(CrewSplitMemberResult member) =>
    new(member.AccountId, member.Role.ToString(), member.RatioWeight, member.Amount);

static IReadOnlyList<string> GetMissingRequiredComponents(
    IReadOnlyDictionary<string, int> quantitiesByItemCode,
    IReadOnlyDictionary<string, int> requiredByItemCode)
{
    return requiredByItemCode
        .Where(requirement =>
        {
            var loaded = quantitiesByItemCode.TryGetValue(requirement.Key, out var quantity) ? quantity : 0;
            return loaded < requirement.Value;
        })
        .Select(requirement => requirement.Key)
        .OrderBy(itemCode => itemCode, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public partial class Program;
