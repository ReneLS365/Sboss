using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public interface IYardCapacityValidator
{
    Task<CommandValidationResult> ValidateAsync(PlaceComponentIntent intent, CancellationToken cancellationToken);
}
