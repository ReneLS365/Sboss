namespace Sboss.Infrastructure.Services;

public sealed class AuthoritativeLoadoutRequirementProvider : IAuthoritativeLoadoutRequirementProvider
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static readonly IReadOnlyDictionary<string, int> KnownSeedRequirements =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["scaffold_blue_frame"] = 1,
            ["scaffold_yellow_deck"] = 1,
            ["scaffold_red_diagonal"] = 1
        };

    public IReadOnlyDictionary<string, int> GetRequiredItemQuantities(Guid levelSeedId)
    {
        if (levelSeedId == KnownSeedId)
        {
            return KnownSeedRequirements;
        }

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
