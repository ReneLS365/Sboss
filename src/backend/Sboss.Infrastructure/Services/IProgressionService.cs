namespace Sboss.Infrastructure.Services;

public interface IProgressionService
{
    Task<ProgressionState> GetStateAsync(Guid accountId, CancellationToken cancellationToken);
    Task<ProgressionAwardResult> AwardFromMatchResultAsync(Guid accountId, Guid matchResultId, CancellationToken cancellationToken);
    bool IsTemplateUnlocked(string templateCode, int playerLevel);
    IReadOnlyList<string> GetUnlockedTemplates(int playerLevel);
    long? GetNextLevelXpRequired(long totalXp);
}
