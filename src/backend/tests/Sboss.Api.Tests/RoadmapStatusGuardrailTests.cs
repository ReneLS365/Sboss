using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    private static readonly Regex CurrentTaskRegex = new(
        @"^- Current task:\s+\*\*(?<step>\d+[A-Z])\s+[—-]\s+.+\*\*$",
        RegexOptions.Multiline);

    private static readonly Regex NextTaskRegex = new(
        @"^- Next task:\s+\*\*(?<step>\d+[A-Z])\s+[—-]\s+.+\*\*$",
        RegexOptions.Multiline);

    private static readonly Regex RoadmapChecklistRegex = new(
        @"^- \[(?: |x|X)\]\s+(?<step>\d+[A-Z])(?:\s+[—-])?\s+(?<title>.+)$",
        RegexOptions.Multiline);

    [Fact]
    public void ValidationScript_PassesForCurrentRepoState()
    {
        var result = RunValidation(ResolveRepoPath("docs/MASTER_STATUS.md"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenNextTaskSkipsAhead()
    {
        var masterStatus = ReadMasterStatus();
        var nextStep = ExtractNextTaskStep(masterStatus);
        var roadmapSteps = CollectRoadmapChecklistStepIdsInOrder(masterStatus);
        var nextStepIndex = roadmapSteps.IndexOf(nextStep);

        Assert.True(nextStepIndex >= 0, $"Expected to find Next task step '{nextStep}' in roadmap checklist.");
        Assert.True(
            nextStepIndex + 1 < roadmapSteps.Count,
            $"Cannot construct skip-ahead fixture because Next task '{nextStep}' has no later roadmap step.");

        var skippedAheadStep = roadmapSteps[nextStepIndex + 1];
        var nextTaskLine = FindTaskLine(masterStatus, NextTaskRegex, "Next task");
        var mutatedNextTaskLine = nextTaskLine.Replace(nextStep, skippedAheadStep, StringComparison.Ordinal);
        Assert.NotEqual(nextTaskLine, mutatedNextTaskLine);
        masterStatus = masterStatus.Replace(nextTaskLine, mutatedNextTaskLine, StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(masterStatus));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Next task skips ahead or reorders the roadmap", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenCurrentTaskMissingFromRoadmapChecklist()
    {
        var masterStatus = ReadMasterStatus();
        var currentStep = ExtractCurrentTaskStep(masterStatus);
        var currentChecklistLine = FindRoadmapChecklistLineForStep(masterStatus, currentStep);
        var invalidStep = DetermineInvalidRoadmapStepToken(masterStatus);
        var mutatedChecklistLine = Regex.Replace(
            currentChecklistLine,
            $@"^(- \[(?: |x|X)\]\s+){Regex.Escape(currentStep)}(\b)",
            $"$1{invalidStep}$2");

        Assert.NotEqual(currentChecklistLine, mutatedChecklistLine);
        masterStatus = masterStatus.Replace(currentChecklistLine, mutatedChecklistLine, StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(masterStatus));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Current task step '{currentStep}' is missing", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgresFixture_RequiresExplicitIsolatedTestDatabase()
    {
        var original = Environment.GetEnvironmentVariable("SBOSS_TEST_DATABASE");

        try
        {
            Environment.SetEnvironmentVariable("SBOSS_TEST_DATABASE", null);

            var exception = Assert.Throws<InvalidOperationException>(() => CreateFixtureConnectionString());

            Assert.Contains("SBOSS_TEST_DATABASE must be set", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SBOSS_TEST_DATABASE", original);
        }
    }

    [Fact]
    public void PostgresFixture_RejectsDefaultDevelopmentDatabase()
    {
        var original = Environment.GetEnvironmentVariable("SBOSS_TEST_DATABASE");

        try
        {
            Environment.SetEnvironmentVariable("SBOSS_TEST_DATABASE", "Host=localhost;Port=5432;Database=sboss;Username=test;Password=test");

            var exception = Assert.Throws<InvalidOperationException>(() => CreateFixtureConnectionString());

            Assert.Contains("must not target the default 'sboss' development database", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SBOSS_TEST_DATABASE", original);
        }
    }

    private static string CreateFixtureConnectionString()
    {
        var method = typeof(PostgresDatabaseFixture).GetMethod("ResolveConnectionString", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        try
        {
            return (string)method.Invoke(null, null)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static (int ExitCode, string Output) RunValidation(string masterStatusPath)
    {
        var startInfo = new ProcessStartInfo("python3")
        {
            WorkingDirectory = ResolveRepoPath("."),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add(ResolveRepoPath("scripts/validate-roadmap-status.py"));
        startInfo.ArgumentList.Add("--master-status-file");
        startInfo.ArgumentList.Add(masterStatusPath);

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout + stderr);
    }

    private static string WriteTempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sboss-status-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, contents);
        return path;
    }

    private static string ResolveRepoPath(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.GetFullPath(Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (relativePath == "." && File.Exists(Path.Combine(candidate, "Sboss.sln")))
            {
                return candidate;
            }

            if (relativePath != "." && (File.Exists(candidate) || Directory.Exists(candidate)))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Unable to resolve '{relativePath}' from test base directory.");
    }

    private static string ReadMasterStatus()
    {
        return File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
    }

    private static string ExtractCurrentTaskStep(string masterStatus)
    {
        return ExtractStep(masterStatus, CurrentTaskRegex, "Current task");
    }

    private static string ExtractNextTaskStep(string masterStatus)
    {
        return ExtractStep(masterStatus, NextTaskRegex, "Next task");
    }

    private static string ExtractStep(string masterStatus, Regex regex, string label)
    {
        var match = regex.Match(masterStatus);
        Assert.True(match.Success, $"Expected {label} line in docs/MASTER_STATUS.md.");
        return match.Groups["step"].Value;
    }

    private static string FindTaskLine(string masterStatus, Regex regex, string label)
    {
        var match = regex.Match(masterStatus);
        Assert.True(match.Success, $"Expected {label} line in docs/MASTER_STATUS.md.");
        return match.Value;
    }

    private static List<string> CollectRoadmapChecklistStepIdsInOrder(string masterStatus)
    {
        var matches = RoadmapChecklistRegex.Matches(masterStatus);
        Assert.True(matches.Count > 0, "Expected at least one roadmap checklist step in docs/MASTER_STATUS.md.");

        var orderedSteps = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            orderedSteps.Add(match.Groups["step"].Value);
        }

        return orderedSteps;
    }

    private static string FindRoadmapChecklistLineForStep(string masterStatus, string step)
    {
        foreach (Match match in RoadmapChecklistRegex.Matches(masterStatus))
        {
            if (string.Equals(match.Groups["step"].Value, step, StringComparison.Ordinal))
            {
                return match.Value;
            }
        }

        throw new InvalidOperationException($"Could not find roadmap checklist line for step '{step}'.");
    }

    private static string DetermineInvalidRoadmapStepToken(string masterStatus)
    {
        var roadmapSteps = CollectRoadmapChecklistStepIdsInOrder(masterStatus);
        var candidates = new[] { "0Z", "9Z", "99Z" };

        foreach (var candidate in candidates)
        {
            if (!roadmapSteps.Contains(candidate, StringComparer.Ordinal))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to construct an invalid roadmap step token.");
    }
}
