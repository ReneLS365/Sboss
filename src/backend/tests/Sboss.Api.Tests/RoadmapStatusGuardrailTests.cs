using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    private const string ActiveTaskId = "P1I-HARDENING-AND-INVARIANTS";
    private static readonly Regex MasterCurrentTaskRegex = new("^- Current task:\\s+\\*\\*(?<step>\\d+[A-Z])\\s+[—-]\\s+(?<title>.+)\\*\\*$", RegexOptions.Multiline);
    private static readonly Regex MasterNextTaskRegex = new("^- Next task:\\s+\\*\\*(?<step>\\d+[A-Z])\\s+[—-]\\s+(?<title>.+)\\*\\*$", RegexOptions.Multiline);
    private static readonly Regex PlanInProgressTaskIdRegex = new("## Task Record — (?<taskId>.+?)\\r?\\n(?:.|\\r?\\n)*?- \\*\\*Status:\\*\\* IN_PROGRESS", RegexOptions.Singleline);

    [Fact]
    public void ValidationScript_PassesForCurrentRepoState()
    {
        var result = RunValidation(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            taskId: ActiveTaskId);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenMultipleTasksAreInProgress()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** #11 (merged)", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** #11 (merged)", StringComparison.Ordinal);
        plans = plans.Replace("- **Status:** DONE\n- **Branch:** work\n- **PR:** #10 (merged)", "- **Status:** IN_PROGRESS\n- **Branch:** work\n- **PR:** #10 (merged)", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: ActiveTaskId);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("More than one task is marked IN_PROGRESS", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenDoneTaskLacksPrReference()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        plans = plans.Replace("- **PR:** #10 (merged)", "- **PR:** Missing", StringComparison.Ordinal);

        var result = RunValidation(WriteTempFile(plans), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: ActiveTaskId);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("without a required PR reference", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_FailsWhenNextTaskSkipsAhead()
    {
        var masterStatus = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));
        masterStatus = masterStatus.Replace("- Next task: **2A — Tick model + schema**", "- Next task: **2B — Tick processor skeleton**", StringComparison.Ordinal);

        var result = RunValidation(ResolveRepoPath("PLANS.md"), WriteTempFile(masterStatus), taskId: ActiveTaskId);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Next task skips ahead or reorders the roadmap", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_AllowsNextTaskAfterInProgressActiveTask()
    {
        var result = RunValidation(ResolveRepoPath("PLANS.md"), ResolveRepoPath("docs/MASTER_STATUS.md"), taskId: ActiveTaskId);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("validation passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationScript_AllowsCrossPhaseNextTaskForFinalTaskInPhase()
    {
        const string masterStatus = """
# Sboss — Master Status

## Current Position
- Current phase: **Phase 1 — Authoritative Core Domain**
- Completed phase: **Phase 0 — Foundation / Bootstrap**
- Current task: **1I — Hardening + invariants**
- Next task: **2A — Tick model + schema**

---

## Status overview
- [x] Phase 0 — Foundation / Bootstrap
- [ ] Phase 1 — Authoritative Core Domain
- [ ] Phase 2 — Deterministic Tick Engine

---

## Phase 1 — Authoritative Core Domain
- [x] 1H Integration tests for exploit resistance
- [ ] 1I Hardening + invariants

## Phase 2 — Deterministic Tick Engine
- [ ] 2A Tick model + schema
- [ ] 2B Tick processor skeleton
""";

        const string plans = """
# Sboss Phase Plan

## Task Record — P1I-HARDENING-AND-INVARIANTS
- **Task ID:** P1I-HARDENING-AND-INVARIANTS
- **Title:** Phase 1I hardening and invariants
- **Status:** IN_PROGRESS
- **PR:** Draft PR prepared via make_pr
""";

        var result = RunValidation(WriteTempFile(plans), WriteTempFile(masterStatus), taskId: "P1I-HARDENING-AND-INVARIANTS");

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
    public void AdvanceScript_DryRun_AllowsDirectRoadmapSuccessor()
    {
        var state = ReadRepoRoadmapState();

        var result = RunAdvance(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            ResolveRepoPath("README.md"),
            ResolveRepoPath("src/backend/tests/Sboss.Api.Tests/RoadmapStatusGuardrailTests.cs"),
            expectedCurrentTask: state.CurrentTaskId,
            nextTask: state.NextStep,
            mergedPr: "#999",
            dryRun: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Roadmap advancement check passed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdvanceScript_FailsWhenMergedPrReferenceIsMissing()
    {
        var state = ReadRepoRoadmapState();

        var result = RunAdvance(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            ResolveRepoPath("README.md"),
            ResolveRepoPath("src/backend/tests/Sboss.Api.Tests/RoadmapStatusGuardrailTests.cs"),
            expectedCurrentTask: state.CurrentTaskId,
            nextTask: state.NextStep,
            mergedPr: "missing",
            dryRun: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Merged PR reference must be provided", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdvanceScript_FailsWhenNextTaskSkipsRoadmapSuccessor()
    {
        var state = ReadRepoRoadmapState();

        var result = RunAdvance(
            ResolveRepoPath("PLANS.md"),
            ResolveRepoPath("docs/MASTER_STATUS.md"),
            ResolveRepoPath("README.md"),
            ResolveRepoPath("src/backend/tests/Sboss.Api.Tests/RoadmapStatusGuardrailTests.cs"),
            expectedCurrentTask: state.CurrentTaskId,
            nextTask: "99Z",
            mergedPr: "#999",
            dryRun: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("not direct roadmap successor", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdvanceScript_CrossPhasePromotion_UpdatesPlansMetadataAndStatusSurfaces()
    {
        const string plans = """
# Sboss Phase Plan

## Current Phase
- **Current_phase:** 1 (Authoritative Core Domain)
- **Execution_mode:** Follow `docs/MASTER_STATUS.md` and complete roadmap tasks in sequence.

---

## Task Record — P1I-HARDENING-AND-INVARIANTS
- **Task ID:** P1I-HARDENING-AND-INVARIANTS
- **Title:** Phase 1I hardening and invariants
- **Status:** IN_PROGRESS
- **Branch:** work
- **PR:** Draft PR pending
""";

        const string masterStatus = """
# Sboss — Master Status

## Current Position
- Current phase: **Phase 1 — Authoritative Core Domain**
- Completed phase: **Phase 0 — Foundation / Bootstrap**
- Current task: **1I — Hardening + invariants**
- Next task: **2A — Tick model + schema**

---

## Status overview
- [x] Phase 0 — Foundation / Bootstrap
- [ ] Phase 1 — Authoritative Core Domain
- [ ] Phase 2 — Deterministic Tick Engine

---

## Phase 1 — Authoritative Core Domain
- [x] 1H Integration tests for exploit resistance
- [ ] 1I Hardening + invariants

## Phase 2 — Deterministic Tick Engine
- [ ] 2A Tick model + schema
- [ ] 2B Tick processor skeleton
""";

        const string readme = """
# Sboss

Current roadmap position:
- **Current task: 1I — Hardening + invariants**
- **Next task: 2A — Tick model + schema**
""";

        const string guardrail = """
namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    private const string ActiveTaskId = "P1I-HARDENING-AND-INVARIANTS";
}
""";

        var fixture = WriteTempRoadmapFixture(plans, masterStatus, readme, guardrail);
        var advance = RunAdvance(
            fixture.PlansPath,
            fixture.MasterStatusPath,
            fixture.ReadmePath,
            fixture.GuardrailPath,
            expectedCurrentTask: "P1I-HARDENING-AND-INVARIANTS",
            nextTask: "2A",
            mergedPr: "#28",
            dryRun: false);

        Assert.Equal(0, advance.ExitCode);

        var updatedPlans = File.ReadAllText(fixture.PlansPath);
        var updatedMaster = File.ReadAllText(fixture.MasterStatusPath);
        var updatedReadme = File.ReadAllText(fixture.ReadmePath);
        var updatedGuardrail = File.ReadAllText(fixture.GuardrailPath);

        Assert.Contains("- **Current_phase:** 2 (Deterministic Tick Engine)", updatedPlans, StringComparison.Ordinal);
        Assert.Contains("- Current task: **2A — Tick model + schema**", updatedMaster, StringComparison.Ordinal);
        Assert.Contains("- Next task: **2B — Tick processor skeleton**", updatedMaster, StringComparison.Ordinal);
        Assert.Contains("- **Current task: 2A — Tick model + schema**", updatedReadme, StringComparison.Ordinal);
        Assert.Contains("private const string ActiveTaskId = \"P2A-TICK-MODEL-AND-SCHEMA\";", updatedGuardrail, StringComparison.Ordinal);

        var validation = RunValidation(fixture.PlansPath, fixture.MasterStatusPath, taskId: "P2A-TICK-MODEL-AND-SCHEMA");
        Assert.Equal(0, validation.ExitCode);
    }

    [Fact]
    public void AdvanceScript_SamePhasePromotion_KeepsPlansCurrentPhaseMetadata()
    {
        const string plans = """
# Sboss Phase Plan

## Current Phase
- **Current_phase:** 2 (Deterministic Tick Engine)
- **Execution_mode:** Follow `docs/MASTER_STATUS.md` and complete roadmap tasks in sequence.

---

## Task Record — P2A-TICK-MODEL-AND-SCHEMA
- **Task ID:** P2A-TICK-MODEL-AND-SCHEMA
- **Title:** Phase 2A tick model and schema
- **Status:** IN_PROGRESS
- **Branch:** work
- **PR:** Draft PR pending
""";

        const string masterStatus = """
# Sboss — Master Status

## Current Position
- Current phase: **Phase 2 — Deterministic Tick Engine**
- Completed phase: **Phase 1 — Authoritative Core Domain**
- Current task: **2A — Tick model + schema**
- Next task: **2B — Tick processor skeleton**

---

## Status overview
- [x] Phase 1 — Authoritative Core Domain
- [ ] Phase 2 — Deterministic Tick Engine

---

## Phase 2 — Deterministic Tick Engine
- [ ] 2A Tick model + schema
- [ ] 2B Tick processor skeleton
- [ ] 2C Move job progression into ticks
""";

        const string readme = """
# Sboss

Current roadmap position:
- **Current task: 2A — Tick model + schema**
- **Next task: 2B — Tick processor skeleton**
""";

        const string guardrail = """
namespace Sboss.Api.Tests;

public sealed class RoadmapStatusGuardrailTests
{
    private const string ActiveTaskId = "P2A-TICK-MODEL-AND-SCHEMA";
}
""";

        var fixture = WriteTempRoadmapFixture(plans, masterStatus, readme, guardrail);
        var advance = RunAdvance(
            fixture.PlansPath,
            fixture.MasterStatusPath,
            fixture.ReadmePath,
            fixture.GuardrailPath,
            expectedCurrentTask: "P2A-TICK-MODEL-AND-SCHEMA",
            nextTask: "2B",
            mergedPr: "#28",
            dryRun: false);

        Assert.Equal(0, advance.ExitCode);

        var updatedPlans = File.ReadAllText(fixture.PlansPath);
        Assert.Contains("- **Current_phase:** 2 (Deterministic Tick Engine)", updatedPlans, StringComparison.Ordinal);

        var validation = RunValidation(fixture.PlansPath, fixture.MasterStatusPath, taskId: "P2B-TICK-PROCESSOR-SKELETON");
        Assert.Equal(0, validation.ExitCode);
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

    private static (int ExitCode, string Output) RunAdvance(
        string plansPath,
        string masterStatusPath,
        string readmePath,
        string guardrailPath,
        string expectedCurrentTask,
        string nextTask,
        string mergedPr,
        bool dryRun)
    {
        var startInfo = new ProcessStartInfo("python3")
        {
            WorkingDirectory = ResolveRepoPath("."),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add(ResolveRepoPath("scripts/advance-roadmap-status.py"));
        startInfo.ArgumentList.Add("--plans-file");
        startInfo.ArgumentList.Add(plansPath);
        startInfo.ArgumentList.Add("--master-status-file");
        startInfo.ArgumentList.Add(masterStatusPath);
        startInfo.ArgumentList.Add("--readme-file");
        startInfo.ArgumentList.Add(readmePath);
        startInfo.ArgumentList.Add("--guardrail-test-file");
        startInfo.ArgumentList.Add(guardrailPath);
        startInfo.ArgumentList.Add("--expected-current-task");
        startInfo.ArgumentList.Add(expectedCurrentTask);
        startInfo.ArgumentList.Add("--next-task");
        startInfo.ArgumentList.Add(nextTask);
        startInfo.ArgumentList.Add("--merged-pr");
        startInfo.ArgumentList.Add(mergedPr);

        if (dryRun)
        {
            startInfo.ArgumentList.Add("--dry-run");
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout + stderr);
    }

    private static (string CurrentTaskId, string CurrentStep, string NextStep) ReadRepoRoadmapState()
    {
        var plans = File.ReadAllText(ResolveRepoPath("PLANS.md"));
        var master = File.ReadAllText(ResolveRepoPath("docs/MASTER_STATUS.md"));

        var inProgressTask = PlanInProgressTaskIdRegex.Match(plans);
        Assert.True(inProgressTask.Success, "Expected exactly one IN_PROGRESS task in PLANS.md for test state derivation.");

        var current = MasterCurrentTaskRegex.Match(master);
        var next = MasterNextTaskRegex.Match(master);
        Assert.True(current.Success, "Unable to parse current task from docs/MASTER_STATUS.md.");
        Assert.True(next.Success, "Unable to parse next task from docs/MASTER_STATUS.md.");

        return (
            CurrentTaskId: inProgressTask.Groups["taskId"].Value.Trim(),
            CurrentStep: current.Groups["step"].Value.Trim(),
            NextStep: next.Groups["step"].Value.Trim());
    }

    private static (string PlansPath, string MasterStatusPath, string ReadmePath, string GuardrailPath) WriteTempRoadmapFixture(
        string plans,
        string masterStatus,
        string readme,
        string guardrail)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sboss-advance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "docs"));
        Directory.CreateDirectory(Path.Combine(directory, "src/backend/tests/Sboss.Api.Tests"));

        var plansPath = Path.Combine(directory, "PLANS.md");
        var masterPath = Path.Combine(directory, "docs/MASTER_STATUS.md");
        var readmePath = Path.Combine(directory, "README.md");
        var guardrailPath = Path.Combine(directory, "src/backend/tests/Sboss.Api.Tests/RoadmapStatusGuardrailTests.cs");

        File.WriteAllText(plansPath, plans);
        File.WriteAllText(masterPath, masterStatus);
        File.WriteAllText(readmePath, readme);
        File.WriteAllText(guardrailPath, guardrail);

        return (plansPath, masterPath, readmePath, guardrailPath);
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
