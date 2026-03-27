using Sboss.Infrastructure.Services;

namespace Sboss.Api.Tests;

public sealed class CommandValidationQueueTests
{
    private static readonly Guid KnownSeedId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly FakeYardCapacityProvider _yardCapacityProvider = new();
    private readonly FakeComponentCapacityProvider _componentCapacityProvider = new();

    [Fact]
    public async Task ValidatePlaceComponentIntent_Accepts_WhenRequiredCapacityIsBelowRemainingCapacity()
    {
        var queue = CreateQueue();
        _yardCapacityProvider.CapacityBySeedId[KnownSeedId] = 5;
        _componentCapacityProvider.CapacityByComponentId["scaffold_blue_frame"] = 2;

        var result = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2026-03-27T00:00:00Z","ComponentId":"scaffold_blue_frame"}
            """,
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("accepted", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_Accepts_WhenRequiredCapacityEqualsRemainingCapacity()
    {
        var queue = CreateQueue();
        _yardCapacityProvider.CapacityBySeedId[KnownSeedId] = 3;
        _componentCapacityProvider.CapacityByComponentId["scaffold_yellow_deck"] = 3;

        var result = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2026-03-27T00:00:00Z","ComponentId":"scaffold_yellow_deck"}
            """,
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("accepted", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_Rejects_WhenRequiredCapacityExceedsRemainingCapacity()
    {
        var queue = CreateQueue();
        _yardCapacityProvider.CapacityBySeedId[KnownSeedId] = 2;
        _componentCapacityProvider.CapacityByComponentId["scaffold_red_diagonal"] = 5;

        var result = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2026-03-27T00:00:00Z","ComponentId":"scaffold_red_diagonal"}
            """,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("yard_capacity_exceeded", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_RejectsUnknownComponentId()
    {
        var queue = CreateQueue();
        _yardCapacityProvider.CapacityBySeedId[KnownSeedId] = 5;

        var result = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2026-03-27T00:00:00Z","ComponentId":"unknown_component"}
            """,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("unknown_component_id", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_RejectsUnknownSeedId()
    {
        var queue = CreateQueue();
        _componentCapacityProvider.CapacityByComponentId["scaffold_blue_frame"] = 1;

        var result = await queue.ValidatePlaceComponentIntentAsync(
            """
            {"SeedId":"99999999-9999-9999-9999-999999999999","Timestamp":"2026-03-27T00:00:00Z","ComponentId":"scaffold_blue_frame"}
            """,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("unknown_seed_id", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_RejectsMalformedIntentJson()
    {
        var queue = CreateQueue();

        var result = await queue.ValidatePlaceComponentIntentAsync(
            """
            {"SeedId":"dddddddd-dddd-dddd-dddd-dddddddddddd","Timestamp":"2026-03-27T00:00:00Z"
            """,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("malformed_intent", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_RejectsNonDeserializableIntentJson()
    {
        var queue = CreateQueue();

        var result = await queue.ValidatePlaceComponentIntentAsync(
            """
            {"SeedId":"dddddddd-dddd-dddd-dddd-dddddddddddd","Timestamp":"2026-03-27T00:00:00Z"}
            """,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("malformed_intent", result.Code);
    }

    [Fact]
    public async Task ValidatePlaceComponentIntent_UsesAuthoritativeBackendDataRegardlessOfTimestampValue()
    {
        var queue = CreateQueue();
        _yardCapacityProvider.CapacityBySeedId[KnownSeedId] = 1;
        _componentCapacityProvider.CapacityByComponentId["scaffold_red_diagonal"] = 5;

        var oldTimestampResult = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2020-01-01T00:00:00Z","ComponentId":"scaffold_red_diagonal"}
            """,
            CancellationToken.None);

        var futureTimestampResult = await queue.ValidatePlaceComponentIntentAsync(
            $$"""
            {"SeedId":"{{KnownSeedId}}","Timestamp":"2036-01-01T00:00:00Z","ComponentId":"scaffold_red_diagonal"}
            """,
            CancellationToken.None);

        Assert.False(oldTimestampResult.Accepted);
        Assert.False(futureTimestampResult.Accepted);
        Assert.Equal("yard_capacity_exceeded", oldTimestampResult.Code);
        Assert.Equal("yard_capacity_exceeded", futureTimestampResult.Code);
    }

    private ICommandValidationQueue CreateQueue()
    {
        var validator = new YardCapacityValidator(_yardCapacityProvider, _componentCapacityProvider);
        return new CommandValidationQueue(validator);
    }

    private sealed class FakeYardCapacityProvider : IAuthoritativeYardCapacityProvider
    {
        public Dictionary<Guid, int> CapacityBySeedId { get; } = new();

        public Task<int?> GetRemainingCapacityAsync(Guid seedId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CapacityBySeedId.TryGetValue(seedId, out var capacity) ? (int?)capacity : null);
        }
    }

    private sealed class FakeComponentCapacityProvider : IAuthoritativeComponentCapacityProvider
    {
        public Dictionary<string, int> CapacityByComponentId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<int?> GetRequiredCapacityAsync(string componentId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CapacityByComponentId.TryGetValue(componentId, out var capacity) ? (int?)capacity : null);
        }
    }
}
