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

    public CommandValidationQueue(IYardCapacityValidator yardCapacityValidator)
    {
        _yardCapacityValidator = yardCapacityValidator;
    }

    public async Task<CommandValidationResult> ValidatePlaceComponentIntentAsync(string rawIntentJson, CancellationToken cancellationToken)
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

        return await _yardCapacityValidator.ValidateAsync(intent, cancellationToken);
    }
}
