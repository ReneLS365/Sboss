namespace Sboss.Infrastructure.Services;

public interface IAuthoritativeComponentCatalog
{
    bool TryGetComponent(string itemCode, out AuthoritativeComponentDefinition component);
    IReadOnlyCollection<AuthoritativeComponentDefinition> GetSupportedComponents();
}

public sealed record AuthoritativeComponentDefinition(
    string ItemCode,
    int UnitCapacity,
    long PurchaseCost);
