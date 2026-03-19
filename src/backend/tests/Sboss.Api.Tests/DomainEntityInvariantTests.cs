using Sboss.Domain.Entities;

namespace Sboss.Api.Tests;

public sealed class DomainEntityInvariantTests
{
    [Fact]
    public void Account_Create_WithInvalidExternalRef_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => Account.Create(Guid.NewGuid(), "   ", now));
    }

    [Fact]
    public void Season_Create_WithInvalidTimeRange_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            Season.Create(Guid.NewGuid(), "Season 1", now, now, true, now));
    }

    [Fact]
    public void LevelSeed_Create_WithGoldTimeGreaterThanParTime_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            LevelSeed.Create(Guid.NewGuid(), "seed", "urban", "template", "objective", "{}", 1000, 1001, now));
    }

    [Fact]
    public void MatchResult_Create_WithInvalidIdentifiers_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            MatchResult.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 1, 1, 0, 0, now));
    }

    [Theory]
    [InlineData(-1, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, -1, 0)]
    [InlineData(1, 1, 0, -1)]
    public void MatchResult_Create_WithInvalidNumericValues_Throws(int score, int clearTimeMs, int comboMax, int penalties)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            MatchResult.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), score, clearTimeMs, comboMax, penalties, now));
    }

    [Fact]
    public void DomainEntities_DoNotExposePublicSettersOnCriticalState()
    {
        AssertNoPublicSetter<Account>(nameof(Account.AccountId), nameof(Account.ExternalRef), nameof(Account.CreatedAt), nameof(Account.UpdatedAt), nameof(Account.Version));
        AssertNoPublicSetter<Season>(nameof(Season.SeasonId), nameof(Season.Name), nameof(Season.StartsAt), nameof(Season.EndsAt), nameof(Season.IsActive), nameof(Season.CreatedAt), nameof(Season.UpdatedAt), nameof(Season.Version));
        AssertNoPublicSetter<LevelSeed>(nameof(LevelSeed.LevelSeedId), nameof(LevelSeed.SeedValue), nameof(LevelSeed.Biome), nameof(LevelSeed.Template), nameof(LevelSeed.Objective), nameof(LevelSeed.ModifiersJson), nameof(LevelSeed.ParTimeMs), nameof(LevelSeed.GoldTimeMs), nameof(LevelSeed.Version), nameof(LevelSeed.CreatedAt), nameof(LevelSeed.UpdatedAt));
        AssertNoPublicSetter<MatchResult>(nameof(MatchResult.MatchResultId), nameof(MatchResult.AccountId), nameof(MatchResult.SeasonId), nameof(MatchResult.LevelSeedId), nameof(MatchResult.Score), nameof(MatchResult.ClearTimeMs), nameof(MatchResult.ComboMax), nameof(MatchResult.Penalties), nameof(MatchResult.ValidationStatus), nameof(MatchResult.CreatedAt), nameof(MatchResult.UpdatedAt), nameof(MatchResult.Version));
    }

    private static void AssertNoPublicSetter<T>(params string[] propertyNames)
    {
        var type = typeof(T);
        foreach (var propertyName in propertyNames)
        {
            var property = type.GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.False(property!.SetMethod?.IsPublic ?? false, $"{type.Name}.{propertyName} should not have a public setter.");
        }
    }
}
