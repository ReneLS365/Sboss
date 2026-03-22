using System.Diagnostics;
using System.Reflection;

namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    [Fact]
    public void ValidationScript_PassesForCurrentRepoState()
    {
        var result = RunValidation(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            taskId: "P1F-COMPANY-JOB-APPLICATION-SERVICES");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenMultipleTasksAreInProgress()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** #11 (merged)", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** #11 (merged)", StringComparison.Ordinal);
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** #10 (merged)", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** #10 (merged)", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: "P1F-COMPANY-JOB-APPLICATION-SERVICES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("More than one task is marked IN_PROGRESS", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenDoneTaskLacksPrReference()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **PR:** #10 (merged)", "- **PR:** Missing", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: "P1F-COMPANY-JOB-APPLICATION-SERVICES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("without a required PR reference", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenNextTaskSkipsAhead()
    {
        var masterStatus = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
        masterStatus = masterStatus.Replace("- Next task: **1G — First vertical slice HTTP endpoints**", "- Next task: **1H — Integration tests for exploit resistance**", StringComparison.Ordinal);

        var result = RunValidation(ResolveRepoPath("PLANS.md"), WriteTempFile(masterStatus), taskId: "P1F-COMPANY-JOB-APPLICATION-SERVICES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Next task skips ahead or reorders the roadmap", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_AllowsNextTaskAfterInProgressActiveTask()
    {
        var result = RunValidation(ResolveRepoPath("PLANS.md"), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: "P1F-COMPANY-JOB-APPLICATION-SERVICES");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_UsesCurrentPhaseTaskList()
    {
        const string masterStatus = """
# Sboss — Master Status

## Current Position
- Current phase: **Phase 2 — Deterministic Tick Engine**
- Completed phase: **Phase 1 — Authoritative Core Domain**
- Current task: **2A — Tick model + schema**
- Next task: **2B — Tick processor skeleton**

---

## Status overview
- [x] Phase 0 — Foundation / Bootstrap
- [x] Phase 1 — Authoritative Core Domain
- [ ] Phase 2 — Deterministic Tick Engine

---

## Phase 1 — Authoritative Core Domain
- [x] 1A Domain entities + contracts
- [x] 1B Database schema + migration baseline
- [x] 1C Core repositories
- [x] 1D Economy transaction service

## Phase 2 — Deterministic Tick Engine
- [ ] 2A Tick model + schema
- [ ] 2B Tick processor skeleton
- [ ] 2C Move job progression into ticks
""";

        const string plans = """
# Sboss Phase Plan

## Task Record — P2A-TICK-MODEL-AND-SCHEMA
- **Task ID:** P2A-TICK-MODEL-AND-SCHEMA
- **Title:** Phase 2A tick model and schema
- **Status:** IN_PROGRESS
- **PR:** Draft PR prepared via make_pr
""";

        var result = RunValidation(WriteTempFile(plans), WriteTempFile(masterStatus), taskId: "P2A-TICK-MODEL-AND-SCHEMA");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
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

    private static (int ExitCode, string Output) RunValidation(string plansPath, string masterStatusPath, string taskId)
    {
        var startInfo = new ProcessStartInfo("python3")
        {
            WorkingDirectory = ResolveRepoPath("."),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add(ResolveRepoPath("scripts/validate-roadmap-status.py"));
        startInfo.ArgumentList.Add("--plans-file");
        startInfo.ArgumentList.Add(plansPath);
        startInfo.ArgumentList.Add("--master-status-file");
        startInfo.ArgumentList.Add(masterStatusPath);
        startInfo.ArgumentList.Add("--task-id");
        startInfo.ArgumentList.Add(taskId);

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
