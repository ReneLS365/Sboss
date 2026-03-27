namespace Sboss.Infrastructure.Services;

public interface IAuthoritativeComponentCapacityProvider
{
    Task<int?> GetRequiredCapacityAsync(string componentId, CancellationToken cancellationToken);
}
