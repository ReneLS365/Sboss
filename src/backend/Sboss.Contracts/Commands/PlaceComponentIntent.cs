namespace Sboss.Contracts.Commands;

public sealed record PlaceComponentIntent
{
    public required Guid SeedId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string ComponentId { get; init; }
}
