using System.Diagnostics;

namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    [Fact]
    public void ValidationScript_PassesForCurrentRepoState()
    {
        var result = RunValidation(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            taskId: "P1C-CORE-REPOSITORIES");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenMultipleTasksAreInProgress()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** Draft PR prepared via make_pr", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** Draft PR prepared via make_pr", StringComparison.Ordinal);
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** #10 (merged)", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** #10 (merged)", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: "P1C-CORE-REPOSITORIES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("More than one task is marked IN_PROGRESS", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenDoneTaskLacksPrReference()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **PR:** #10 (merged)", "- **PR:** Missing", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: "P1C-CORE-REPOSITORIES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("without a required PR reference", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenNextTaskSkipsAhead()
    {
        var masterStatus = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
        masterStatus = masterStatus.Replace("- Next task: **1D — Economy transaction service**", "- Next task: **1E — Contract job state machine**", StringComparison.Ordinal);

        var result = RunValidation(ResolveRepoPath("PLANS.md"), WriteTempFile(masterStatus), taskId: "P1C-CORE-REPOSITORIES");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Next task skips ahead or reorders the roadmap", result.Output, StringComparison.OrdinalIgnoreCase);
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
