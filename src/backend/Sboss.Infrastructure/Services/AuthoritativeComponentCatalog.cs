namespace Sboss.Infrastructure.Services;

public sealed class AuthoritativeComponentCatalog : IAuthoritativeComponentCatalog
{
    private static readonly IReadOnlyDictionary<string, AuthoritativeComponentDefinition> Components =
        new Dictionary<string, AuthoritativeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["scaffold_blue_frame"] = new("scaffold_blue_frame", 2, 10),
            ["scaffold_yellow_deck"] = new("scaffold_yellow_deck", 3, 15),
            ["scaffold_red_diagonal"] = new("scaffold_red_diagonal", 5, 25)
        };

    public bool TryGetComponent(string itemCode, out AuthoritativeComponentDefinition component)
    {
        return Components.TryGetValue(itemCode, out component!);
    }

    public IReadOnlyCollection<AuthoritativeComponentDefinition> GetSupportedComponents()
    {
        return Components.Values.ToArray();
    }
}
