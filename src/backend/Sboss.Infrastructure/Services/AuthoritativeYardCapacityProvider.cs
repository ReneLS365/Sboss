using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure.Services;

public sealed class AuthoritativeYardCapacityProvider : IAuthoritativeYardCapacityProvider
{
    private static readonly Dictionary<string, int> RemainingCapacityByTemplate =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["template_alpha"] = 5
        };

    private readonly ILevelSeedRepository _levelSeedRepository;

    public AuthoritativeYardCapacityProvider(ILevelSeedRepository levelSeedRepository)
    {
        _levelSeedRepository = levelSeedRepository;
    }

    public async Task<int?> GetRemainingCapacityAsync(Guid seedId, CancellationToken cancellationToken)
    {
        var seed = await _levelSeedRepository.GetByIdAsync(seedId, cancellationToken);
        if (seed is null)
        {
            return null;
        }

        if (RemainingCapacityByTemplate.TryGetValue(seed.Template, out var remainingCapacity))
        {
            return remainingCapacity;
        }

        return null;
    }
}
