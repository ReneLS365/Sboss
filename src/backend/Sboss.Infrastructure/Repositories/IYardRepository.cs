using Sboss.Infrastructure.Services;

namespace Sboss.Infrastructure.Repositories;

public interface IYardRepository
{
    Task<YardStateSnapshot?> GetSnapshotAsync(Guid accountId, IReadOnlyCollection<AuthoritativeComponentDefinition> supportedComponents, CancellationToken cancellationToken);
    Task<PurchaseResult> PurchaseAsync(
        Guid accountId,
        AuthoritativeComponentDefinition component,
        int quantity,
        IReadOnlyCollection<AuthoritativeComponentDefinition> supportedComponents,
        CancellationToken cancellationToken);
    Task ApplyWearAsync(Guid accountId, IReadOnlyDictionary<string, long> wearByItemCode, CancellationToken cancellationToken);
}

public sealed record YardStateSnapshot(
    Guid AccountId,
    int MaxCapacity,
    int UsedCapacity,
    int RemainingCapacity,
    long CoinBalance,
    IReadOnlyDictionary<string, YardInventoryState> InventoryByItemCode);

public sealed record YardInventoryState(
    int OwnedQuantity,
    int UsableQuantity,
    int DamagedQuantity,
    long TotalIntegrityBps);

public sealed record PurchaseResult(
    bool Success,
    string? FailureCode,
    string? FailureMessage,
    YardStateSnapshot? Snapshot,
    int OwnedQuantity);
