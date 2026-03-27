using System.Diagnostics;
using System.Reflection;

namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
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
        var masterStatus = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
        masterStatus = masterStatus.Replace(
            "- Next task: **2E — Scaffold Assembly Rules**",
            "- Next task: **2F — Vertical Slice Test**",
            StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(masterStatus));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Next task skips ahead or reorders the roadmap", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenCurrentTaskMissingFromRoadmapChecklist()
    {
        var masterStatus = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
        masterStatus = masterStatus.Replace(
            "- [ ] 2D Scoring Engine: Server-autoritativ beregning af stabilitet, combo-multiplier og tid.",
            "- [ ] 2Z Scoring Engine: Server-autoritativ beregning af stabilitet, combo-multiplier og tid.",
            StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(masterStatus));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Current task step '2D' is missing", result.Output, StringComparison.OrdinalIgnoreCase);
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
}
