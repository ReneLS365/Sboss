namespace Sboss.Infrastructure.Services;

public interface IAuthoritativeYardCapacityProvider
{
    Task<int?> GetRemainingCapacityAsync(Guid seedId, CancellationToken cancellationToken);
}
