using System.Text.Json;
using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public sealed class CommandValidationQueue : ICommandValidationQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly IYardCapacityValidator _yardCapacityValidator;
    private readonly IScaffoldAssemblyRulesValidator _scaffoldAssemblyRulesValidator;

    public CommandValidationQueue(
        IYardCapacityValidator yardCapacityValidator,
        IScaffoldAssemblyRulesValidator scaffoldAssemblyRulesValidator)
    {
        _yardCapacityValidator = yardCapacityValidator;
        _scaffoldAssemblyRulesValidator = scaffoldAssemblyRulesValidator;
    }

    public async Task<CommandValidationResult> ValidatePlaceComponentIntentAsync(
        string rawIntentJson,
        IReadOnlyCollection<string>? acceptedComponentIdsInSequence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawIntentJson))
        {
            return CommandValidationResult.Reject("malformed_intent", "Intent JSON is required.");
        }

        PlaceComponentIntent? intent;

        try
        {
            intent = JsonSerializer.Deserialize<PlaceComponentIntent>(rawIntentJson, SerializerOptions);
        }
        catch (JsonException)
        {
            return CommandValidationResult.Reject("malformed_intent", "Intent JSON is malformed or missing required fields.");
        }

        if (intent is null)
        {
            return CommandValidationResult.Reject("malformed_intent", "Intent JSON is malformed or missing required fields.");
        }

        var assemblyResult = _scaffoldAssemblyRulesValidator.Validate(intent, acceptedComponentIdsInSequence);
        if (!assemblyResult.Accepted)
        {
            return assemblyResult;
        }

        return await _yardCapacityValidator.ValidateAsync(intent, cancellationToken);
    }
}
