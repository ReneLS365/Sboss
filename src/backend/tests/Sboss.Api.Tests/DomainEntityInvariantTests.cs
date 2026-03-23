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
    public void Account_Create_WithNullExternalRef_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentNullException>(() => Account.Create(Guid.NewGuid(), null!, now));
    }

    [Fact]
    public void Season_Create_WithInvalidTimeRange_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() =>
            Season.Create(Guid.NewGuid(), "Season 1", now, now, true, now));
    }

    [Fact]
    public void Season_Create_WithNullName_Throws()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentNullException>(() =>
            Season.Create(Guid.NewGuid(), null!, now, now.AddDays(1), true, now));
    }

    [Fact]
    public void Account_UpdateExternalRef_WithInvalidTimestamp_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var account = Account.Create(Guid.NewGuid(), "ext-1", createdAt);

        Assert.Throws<ArgumentException>(() => account.UpdateExternalRef("ext-2", createdAt.AddMinutes(-1)));

        Assert.Equal("ext-1", account.ExternalRef);
        Assert.Equal(createdAt, account.UpdatedAt);
        Assert.Equal(1, account.Version);
    }

    [Fact]
    public void Account_UpdateExternalRef_WithNullValue_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var account = Account.Create(Guid.NewGuid(), "ext-1", createdAt);

        Assert.Throws<ArgumentNullException>(() => account.UpdateExternalRef(null!, createdAt.AddMinutes(1)));

        Assert.Equal("ext-1", account.ExternalRef);
        Assert.Equal(createdAt, account.UpdatedAt);
        Assert.Equal(1, account.Version);
    }

    [Fact]
    public void Season_UpdateSchedule_WithInvalidRange_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startsAt = createdAt.AddDays(1);
        var endsAt = createdAt.AddDays(10);
        var season = Season.Create(Guid.NewGuid(), "Season 1", startsAt, endsAt, true, createdAt);

        Assert.Throws<ArgumentException>(() =>
            season.UpdateSchedule("Season 2", endsAt, startsAt, false, createdAt.AddMinutes(1)));

        Assert.Equal("Season 1", season.Name);
        Assert.Equal(startsAt, season.StartsAt);
        Assert.Equal(endsAt, season.EndsAt);
        Assert.True(season.IsActive);
        Assert.Equal(createdAt, season.UpdatedAt);
        Assert.Equal(1, season.Version);
    }

    [Fact]
    public void Season_UpdateSchedule_WithInvalidTimestamp_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startsAt = createdAt.AddDays(1);
        var endsAt = createdAt.AddDays(10);
        var season = Season.Create(Guid.NewGuid(), "Season 1", startsAt, endsAt, true, createdAt);

        Assert.Throws<ArgumentException>(() =>
            season.UpdateSchedule("Season 2", startsAt.AddDays(1), endsAt.AddDays(1), false, createdAt.AddMinutes(-1)));

        Assert.Equal("Season 1", season.Name);
        Assert.Equal(startsAt, season.StartsAt);
        Assert.Equal(endsAt, season.EndsAt);
        Assert.True(season.IsActive);
        Assert.Equal(createdAt, season.UpdatedAt);
        Assert.Equal(1, season.Version);
    }

    [Fact]
    public void Season_UpdateSchedule_WithNullName_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startsAt = createdAt.AddDays(1);
        var endsAt = createdAt.AddDays(10);
        var season = Season.Create(Guid.NewGuid(), "Season 1", startsAt, endsAt, true, createdAt);

        Assert.Throws<ArgumentNullException>(() =>
            season.UpdateSchedule(null!, startsAt.AddDays(1), endsAt.AddDays(1), false, createdAt.AddMinutes(1)));

        Assert.Equal("Season 1", season.Name);
        Assert.Equal(startsAt, season.StartsAt);
        Assert.Equal(endsAt, season.EndsAt);
        Assert.True(season.IsActive);
        Assert.Equal(createdAt, season.UpdatedAt);
        Assert.Equal(1, season.Version);
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
    public void ContractJob_ValidProgression_IncrementsVersionAndState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var job = ContractJob.Create(Guid.NewGuid(), createdAt);

        var openJob = job.TransitionTo(ContractJobState.Open, createdAt.AddMinutes(1));
        var acceptedJob = openJob.TransitionTo(ContractJobState.Accepted, createdAt.AddMinutes(2));
        var inProgressJob = acceptedJob.TransitionTo(ContractJobState.InProgress, createdAt.AddMinutes(3));
        var completedJob = inProgressJob.TransitionTo(ContractJobState.Completed, createdAt.AddMinutes(4));

        Assert.Equal(ContractJobState.Completed, completedJob.CurrentState);
        Assert.Equal(5, completedJob.Version);
        Assert.Equal(createdAt.AddMinutes(4), completedJob.UpdatedAt);
    }

    [Fact]
    public void ContractJob_InvalidTransition_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var job = ContractJob.Create(Guid.NewGuid(), createdAt);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            job.TransitionTo(ContractJobState.Completed, createdAt.AddMinutes(1)));

        Assert.Equal("Contract job transition from Draft to Completed is not allowed.", exception.Message);
        Assert.Equal(ContractJobState.Draft, job.CurrentState);
        Assert.Equal(1, job.Version);
        Assert.Equal(createdAt, job.UpdatedAt);
    }

    [Fact]
    public void ContractJob_TerminalState_RejectsFurtherTransition()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var job = ContractJob.Create(Guid.NewGuid(), createdAt)
            .TransitionTo(ContractJobState.Open, createdAt.AddMinutes(1))
            .TransitionTo(ContractJobState.Accepted, createdAt.AddMinutes(2))
            .TransitionTo(ContractJobState.InProgress, createdAt.AddMinutes(3))
            .TransitionTo(ContractJobState.Failed, createdAt.AddMinutes(4));

        Assert.Throws<InvalidOperationException>(() =>
            job.TransitionTo(ContractJobState.Open, createdAt.AddMinutes(5)));

        Assert.Equal(ContractJobState.Failed, job.CurrentState);
        Assert.Equal(5, job.Version);
    }

    [Fact]
    public void ContractJobApplication_ValidTransitions_IncrementsVersionAndState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var application = ContractJobApplication.Create(Guid.NewGuid(), Guid.NewGuid(), createdAt);

        var withdrawn = application.Withdraw(createdAt.AddMinutes(1));
        var accepted = application.Accept(createdAt.AddMinutes(2));
        var rejected = application.Reject(createdAt.AddMinutes(3));

        Assert.Equal(ContractJobApplicationStatus.Withdrawn, withdrawn.Status);
        Assert.Equal(2, withdrawn.Version);
        Assert.Equal(ContractJobApplicationStatus.Accepted, accepted.Status);
        Assert.Equal(2, accepted.Version);
        Assert.Equal(ContractJobApplicationStatus.Rejected, rejected.Status);
        Assert.Equal(2, rejected.Version);
    }

    [Fact]
    public void ContractJobApplication_InvalidTransition_DoesNotMutateState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var application = ContractJobApplication.Create(Guid.NewGuid(), Guid.NewGuid(), createdAt).Withdraw(createdAt.AddMinutes(1));

        var exception = Assert.Throws<InvalidOperationException>(() => application.Accept(createdAt.AddMinutes(2)));

        Assert.Equal("Contract job application transition from Withdrawn to Accepted is not allowed.", exception.Message);
        Assert.Equal(ContractJobApplicationStatus.Withdrawn, application.Status);
        Assert.Equal(2, application.Version);
    }

    [Fact]
    public void DomainEntities_DoNotExposePublicSettersOnCriticalState()
    {
        AssertNoPublicSetter<Account>(nameof(Account.AccountId), nameof(Account.ExternalRef), nameof(Account.CreatedAt), nameof(Account.UpdatedAt), nameof(Account.Version));
        AssertNoPublicSetter<Season>(nameof(Season.SeasonId), nameof(Season.Name), nameof(Season.StartsAt), nameof(Season.EndsAt), nameof(Season.IsActive), nameof(Season.CreatedAt), nameof(Season.UpdatedAt), nameof(Season.Version));
        AssertNoPublicSetter<LevelSeed>(nameof(LevelSeed.LevelSeedId), nameof(LevelSeed.SeedValue), nameof(LevelSeed.Biome), nameof(LevelSeed.Template), nameof(LevelSeed.Objective), nameof(LevelSeed.ModifiersJson), nameof(LevelSeed.ParTimeMs), nameof(LevelSeed.GoldTimeMs), nameof(LevelSeed.Version), nameof(LevelSeed.CreatedAt), nameof(LevelSeed.UpdatedAt));
        AssertNoPublicSetter<MatchResult>(nameof(MatchResult.MatchResultId), nameof(MatchResult.AccountId), nameof(MatchResult.SeasonId), nameof(MatchResult.LevelSeedId), nameof(MatchResult.Score), nameof(MatchResult.ClearTimeMs), nameof(MatchResult.ComboMax), nameof(MatchResult.Penalties), nameof(MatchResult.ValidationStatus), nameof(MatchResult.CreatedAt), nameof(MatchResult.UpdatedAt), nameof(MatchResult.Version));
        AssertNoPublicSetter<ContractJob>(nameof(ContractJob.ContractJobId), nameof(ContractJob.OwningAccountId), nameof(ContractJob.CurrentState), nameof(ContractJob.CreatedAt), nameof(ContractJob.UpdatedAt), nameof(ContractJob.Version));
        AssertNoPublicSetter<ContractJobApplication>(nameof(ContractJobApplication.ContractJobApplicationId), nameof(ContractJobApplication.ContractJobId), nameof(ContractJobApplication.ApplicantAccountId), nameof(ContractJobApplication.Status), nameof(ContractJobApplication.CreatedAt), nameof(ContractJobApplication.UpdatedAt), nameof(ContractJobApplication.Version));
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
