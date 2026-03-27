namespace Sboss.Infrastructure.Services;

public sealed class AuthoritativeComponentCapacityProvider : IAuthoritativeComponentCapacityProvider
{
    private static readonly Dictionary<string, int> CapacityByComponentId =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["scaffold_blue_frame"] = 2,
            ["scaffold_yellow_deck"] = 3,
            ["scaffold_red_diagonal"] = 5
        };

    public Task<int?> GetRequiredCapacityAsync(string componentId, CancellationToken cancellationToken)
    {
        if (CapacityByComponentId.TryGetValue(componentId, out var requiredCapacity))
        {
            return Task.FromResult<int?>(requiredCapacity);
        }

        return Task.FromResult<int?>(null);
    }
}
