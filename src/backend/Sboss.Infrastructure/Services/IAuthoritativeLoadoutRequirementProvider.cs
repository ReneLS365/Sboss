namespace Sboss.Infrastructure.Services;

public interface IAuthoritativeLoadoutRequirementProvider
{
    IReadOnlyDictionary<string, int> GetRequiredItemQuantities(Guid levelSeedId);
}
