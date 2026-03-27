using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public interface ICommandValidationQueue
{
    Task<CommandValidationResult> ValidatePlaceComponentIntentAsync(string rawIntentJson, CancellationToken cancellationToken);
}
