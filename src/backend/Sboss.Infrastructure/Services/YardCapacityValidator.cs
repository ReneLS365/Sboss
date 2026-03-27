using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public sealed class YardCapacityValidator : IYardCapacityValidator
{
    private readonly IAuthoritativeYardCapacityProvider _yardCapacityProvider;
    private readonly IAuthoritativeComponentCapacityProvider _componentCapacityProvider;

    public YardCapacityValidator(
        IAuthoritativeYardCapacityProvider yardCapacityProvider,
        IAuthoritativeComponentCapacityProvider componentCapacityProvider)
    {
        _yardCapacityProvider = yardCapacityProvider;
        _componentCapacityProvider = componentCapacityProvider;
    }

    public async Task<CommandValidationResult> ValidateAsync(PlaceComponentIntent intent, CancellationToken cancellationToken)
    {
        if (intent.SeedId == Guid.Empty)
        {
            return CommandValidationResult.Reject("unknown_seed_id", "SeedId is required.");
        }

        if (string.IsNullOrWhiteSpace(intent.ComponentId))
        {
            return CommandValidationResult.Reject("unknown_component_id", "ComponentId is required.");
        }

        var remainingCapacity = await _yardCapacityProvider.GetRemainingCapacityAsync(intent.SeedId, cancellationToken);
        if (remainingCapacity is null)
        {
            return CommandValidationResult.Reject("unknown_seed_id", $"SeedId '{intent.SeedId}' does not exist.");
        }

        var requiredCapacity = await _componentCapacityProvider.GetRequiredCapacityAsync(intent.ComponentId, cancellationToken);
        if (requiredCapacity is null)
        {
            return CommandValidationResult.Reject("unknown_component_id", $"ComponentId '{intent.ComponentId}' does not exist.");
        }

        if (requiredCapacity.Value > remainingCapacity.Value)
        {
            return CommandValidationResult.Reject(
                "yard_capacity_exceeded",
                $"Required capacity ({requiredCapacity.Value}) exceeds remaining yard capacity ({remainingCapacity.Value}).");
        }

        return CommandValidationResult.Accept();
    }
}
