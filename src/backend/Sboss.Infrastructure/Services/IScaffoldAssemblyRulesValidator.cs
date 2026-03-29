using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public interface IScaffoldAssemblyRulesValidator
{
    CommandValidationResult Validate(PlaceComponentIntent intent, IReadOnlyCollection<string>? acceptedComponentIdsInSequence);
}
