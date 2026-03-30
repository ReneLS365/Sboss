namespace Sboss.Infrastructure.Repositories;

public interface IFogOfWarRepository
{
    Task<IReadOnlyCollection<string>> GetRevealedKeysAsync(Guid accountId, Guid levelSeedId, CancellationToken cancellationToken);
    Task<bool> RevealAsync(Guid accountId, Guid levelSeedId, string revealKey, DateTimeOffset revealedAt, CancellationToken cancellationToken);
}
