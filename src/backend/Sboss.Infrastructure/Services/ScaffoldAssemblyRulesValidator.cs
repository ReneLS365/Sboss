using Sboss.Contracts.Commands;

namespace Sboss.Infrastructure.Services;

public sealed class ScaffoldAssemblyRulesValidator : IScaffoldAssemblyRulesValidator
{
    private const string BlueFrame = "scaffold_blue_frame";
    private const string YellowDeck = "scaffold_yellow_deck";
    private const string RedDiagonal = "scaffold_red_diagonal";

    public CommandValidationResult Validate(PlaceComponentIntent intent, IReadOnlyCollection<string>? acceptedComponentIdsInSequence)
    {
        var accepted = acceptedComponentIdsInSequence ?? Array.Empty<string>();

        if (string.Equals(intent.ComponentId, BlueFrame, StringComparison.OrdinalIgnoreCase))
        {
            return CommandValidationResult.Accept();
        }

        if (string.Equals(intent.ComponentId, YellowDeck, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsComponent(accepted, BlueFrame)
                ? CommandValidationResult.Accept()
                : CommandValidationResult.Reject(
                    "scaffold_assembly_invalid_sequence",
                    $"'{YellowDeck}' requires a prior '{BlueFrame}' placement.");
        }

        if (string.Equals(intent.ComponentId, RedDiagonal, StringComparison.OrdinalIgnoreCase))
        {
            var hasFrame = ContainsComponent(accepted, BlueFrame);
            var hasDeck = ContainsComponent(accepted, YellowDeck);
            return hasFrame && hasDeck
                ? CommandValidationResult.Accept()
                : CommandValidationResult.Reject(
                    "scaffold_assembly_invalid_sequence",
                    $"'{RedDiagonal}' requires prior '{BlueFrame}' and '{YellowDeck}' placements.");
        }

        return CommandValidationResult.Accept();
    }

    private static bool ContainsComponent(IEnumerable<string> components, string componentId)
    {
        foreach (var component in components)
        {
            if (string.Equals(component, componentId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
