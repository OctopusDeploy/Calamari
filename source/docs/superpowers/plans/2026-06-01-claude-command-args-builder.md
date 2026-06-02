# ClaudeCommandArgsBuilder + max-turns / max-budget-usd Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract CLI argument building into a fluent `ClaudeCommandArgsBuilder`, add `max-turns` (default 10) and `max-budget-usd` (optional) support, and remove stale `MaxTokens`/`ClaudeCodeOptions`.

**Architecture:** The builder produces a CLI args string via fluent API. The behaviour reads variables and drives the builder. The runner receives the built args string plus API token, credentials, and MCP servers — it handles process lifecycle only.

**Tech Stack:** C# / .NET, NUnit, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-06-01-claude-command-args-builder-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `Calamari.AiAgent/Behaviours/ClaudeCommandArgsBuilder.cs` | Create | Fluent builder: `With*()` methods, `Build()` returns args string, owns `EscapeArg`. Does NOT handle `--mcp-config`, `--strict-mcp-config`, or `--debug-file` — those are appended by the runner since it owns working directory and debug file paths. |
| `Calamari.AiAgent/Behaviours/ClaudeCodeCliRunner.cs` | Modify | Remove `BuildArguments`, `ClaudeCodeOptions`. Change `RunAsync` signature to accept args string + config params. Keep process lifecycle, credentials, MCP/skill setup, records. |
| `Calamari.AiAgent/SpecialVariables.cs` | Modify | Add `MaxTurns`, `MaxBudgetUsd`. Remove `MaxTokens`. |
| `Calamari.AiAgent/Behaviours/InvokeClaudeCodeBehaviour.cs` | Modify | Use builder fluent API, read new variables, pass args string to runner. |
| `Calamari.AiAgent.Tests/ClaudeCommandArgsBuilderFixture.cs` | Create | Unit tests for the builder. |
| `Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs` | Modify | Remove `BuildArguments`/`ClaudeCodeOptions` tests (moved to builder fixture). Keep `SetupSkills`, `SetupMcpConfig` tests. |

---

### Task 1: Create ClaudeCommandArgsBuilder with tests for core flags

**Files:**
- Create: `Calamari.AiAgent/Behaviours/ClaudeCommandArgsBuilder.cs`
- Create: `Calamari.AiAgent.Tests/ClaudeCommandArgsBuilderFixture.cs`

- [ ] **Step 1: Write failing tests for core builder behaviour**

Create `Calamari.AiAgent.Tests/ClaudeCommandArgsBuilderFixture.cs`:

```csharp
using Calamari.AiAgent.Behaviours;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class ClaudeCommandArgsBuilderFixture
{
    ClaudeCommandArgsBuilder MinimalBuilder() =>
        new ClaudeCommandArgsBuilder()
            .WithPrompt("test prompt")
            .WithModel("claude-sonnet-4-20250514");

    [Test]
    public void Build_IncludesRequiredFlags()
    {
        var args = MinimalBuilder().Build();

        args.Should().Contain("-p");
        args.Should().Contain("--model claude-sonnet-4-20250514");
        args.Should().Contain("--output-format stream-json");
        args.Should().Contain("--verbose");
        args.Should().Contain("--permission-mode dontAsk");
        args.Should().Contain("--no-session-persistence");
    }

    [Test]
    public void Build_DefaultsMaxTurnsTo10_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().Contain("--max-turns 10");
    }

    [Test]
    public void Build_UsesProvidedMaxTurns_WhenSet()
    {
        var args = MinimalBuilder().WithMaxTurns(5).Build();

        args.Should().Contain("--max-turns 5");
        args.Should().NotContain("--max-turns 10");
    }

    [Test]
    public void Build_OmitsMaxBudgetUsd_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().NotContain("--max-budget-usd");
    }

    [Test]
    public void Build_IncludesMaxBudgetUsd_WhenSet()
    {
        var args = MinimalBuilder().WithMaxBudgetUsd(1.50m).Build();

        args.Should().Contain("--max-budget-usd 1.50");
    }

    [Test]
    public void Build_IncludesAllowedTools_WhenSet()
    {
        var args = MinimalBuilder()
            .WithAllowedTools(new[] { "Read", "Bash" })
            .Build();

        args.Should().Contain("--allowedTools Read,Bash");
    }

    [Test]
    public void Build_OmitsAllowedTools_WhenEmpty()
    {
        var args = MinimalBuilder()
            .WithAllowedTools(new string[0])
            .Build();

        args.Should().NotContain("--allowedTools");
    }

    [Test]
    public void Build_IncludesSystemPrompt_WhenSet()
    {
        var args = MinimalBuilder()
            .WithSystemPrompt("You are helpful")
            .Build();

        args.Should().Contain("--system-prompt");
        args.Should().Contain("You are helpful");
    }

    [Test]
    public void Build_OmitsSystemPrompt_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().NotContain("--system-prompt");
    }

    [Test]
    public void Build_EscapesPromptWithSpaces()
    {
        var args = new ClaudeCommandArgsBuilder()
            .WithPrompt("What is the capital of France?")
            .WithModel("claude-sonnet-4-20250514")
            .Build();

        args.Should().Contain("\"What is the capital of France?\"");
    }

    [Test]
    public void Build_ThrowsWhenPromptNotSet()
    {
        var builder = new ClaudeCommandArgsBuilder()
            .WithModel("claude-sonnet-4-20250514")
            .WithMcpConfig("/tmp/mcp-config.json");

        var act = () => builder.Build();

        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*prompt*");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Calamari.AiAgent.Tests --filter "FullyQualifiedName~ClaudeCommandArgsBuilderFixture" --no-restore -v minimal`
Expected: Build failure — `ClaudeCommandArgsBuilder` does not exist yet.

- [ ] **Step 3: Implement ClaudeCommandArgsBuilder**

Create `Calamari.AiAgent/Behaviours/ClaudeCommandArgsBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calamari.AiAgent.Behaviours
{
    public class ClaudeCommandArgsBuilder
    {
        string? prompt;
        string? model;
        string? systemPrompt;
        int maxTurns = 10;
        decimal? maxBudgetUsd;
        IReadOnlyList<string>? allowedTools;

        public ClaudeCommandArgsBuilder WithPrompt(string prompt)
        {
            this.prompt = prompt;
            return this;
        }

        public ClaudeCommandArgsBuilder WithModel(string model)
        {
            this.model = model;
            return this;
        }

        public ClaudeCommandArgsBuilder WithSystemPrompt(string systemPrompt)
        {
            this.systemPrompt = systemPrompt;
            return this;
        }

        public ClaudeCommandArgsBuilder WithMaxTurns(int maxTurns)
        {
            this.maxTurns = maxTurns;
            return this;
        }

        public ClaudeCommandArgsBuilder WithMaxBudgetUsd(decimal budgetUsd)
        {
            this.maxBudgetUsd = budgetUsd;
            return this;
        }

        public ClaudeCommandArgsBuilder WithAllowedTools(IReadOnlyList<string> tools)
        {
            this.allowedTools = tools;
            return this;
        }

        public string Build()
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new InvalidOperationException("A prompt is required. Call WithPrompt() before Build().");

            var args = new StringBuilder();
            args.Append("-p ");
            args.Append(EscapeArg(prompt));
            args.Append(" --model ");
            args.Append(EscapeArg(model ?? "claude-sonnet-4-20250514"));
            args.Append(" --output-format stream-json");
            args.Append(" --verbose");
            args.Append(" --permission-mode dontAsk");
            args.Append(" --no-session-persistence");

            if (allowedTools != null && allowedTools.Count > 0)
            {
                args.Append(" --allowedTools ");
                args.Append(string.Join(",", allowedTools));
            }

            args.Append($" --max-turns {maxTurns}");

            if (maxBudgetUsd.HasValue)
                args.Append($" --max-budget-usd {maxBudgetUsd.Value.ToString(CultureInfo.InvariantCulture)}");

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                args.Append(" --system-prompt ");
                args.Append(EscapeArg(systemPrompt));
            }

            return args.ToString();
        }

        static string EscapeArg(string arg)
        {
            if (arg.IndexOfAny(new[] { ' ', '"', '\\' }) < 0)
                return arg;

            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Calamari.AiAgent.Tests --filter "FullyQualifiedName~ClaudeCommandArgsBuilderFixture" --no-restore -v minimal`
Expected: All 9 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Calamari.AiAgent/Behaviours/ClaudeCommandArgsBuilder.cs Calamari.AiAgent.Tests/ClaudeCommandArgsBuilderFixture.cs
git commit -m "feat: add ClaudeCommandArgsBuilder with fluent API and tests"
```

---

### Task 2: Update SpecialVariables

**Files:**
- Modify: `Calamari.AiAgent/SpecialVariables.cs`

- [ ] **Step 1: Update SpecialVariables.cs**

Replace `MaxTokens` with `MaxTurns` and add `MaxBudgetUsd`:

In `SpecialVariables.cs`, remove:
```csharp
public const string MaxTokens = "Octopus.Action.Claude.MaxTokens";
```

Add:
```csharp
public const string MaxTurns = "Octopus.Action.Claude.MaxTurns";
public const string MaxBudgetUsd = "Octopus.Action.Claude.MaxBudgetUsd";
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build Calamari.AiAgent --no-restore -v minimal`
Expected: Build failure — `InvokeClaudeCodeBehaviour.cs` references `MaxTokens`. This is expected and will be fixed in Task 3.

- [ ] **Step 3: Commit**

```bash
git add Calamari.AiAgent/SpecialVariables.cs
git commit -m "feat: add MaxTurns and MaxBudgetUsd variables, remove stale MaxTokens"
```

---

### Task 3: Refactor ClaudeCodeCliRunner — remove BuildArguments and ClaudeCodeOptions

**Files:**
- Modify: `Calamari.AiAgent/Behaviours/ClaudeCodeCliRunner.cs`

- [ ] **Step 1: Update RunAsync signature and remove BuildArguments/ClaudeCodeOptions**

Change `ClaudeCodeCliRunner.cs` to:
- Remove the `ClaudeCodeOptions` record entirely.
- Remove the `BuildArguments` static method.
- Remove `EscapeArg` (now lives in the builder). Keep it if `ApplyCredentials` or other runner logic still needs it — check first.
- Change `RunAsync` to accept the built args string plus the individual config values the runner needs:

```csharp
public async Task<string> RunAsync(
    string args,
    string apiToken,
    IReadOnlyDictionary<string, McpServerConfig> mcpServers,
    ProcessCredentials? runAs = null)
```

Update `RunInDirectoryAsync` similarly — it no longer calls `BuildArguments`. Instead it receives the args string and appends the debug file flag (or the debug file is already in the args from the builder — check the spec). Per the spec, the builder handles `--debug-file`, so the runner should NOT append it. The runner still generates the debug file path and passes it to the builder via the behaviour. However, since `RunAsync` receives a pre-built args string, the debug file path generation needs to move to the behaviour.

Revised approach: the runner generates the debug file path, appends `--debug-file` to the args string it receives, then uses the result. This keeps debug file lifecycle (path generation + artifact upload) co-located in the runner.

Updated `RunInDirectoryAsync`:

```csharp
async Task<string> RunInDirectoryAsync(
    string args,
    string apiToken,
    string workingDir,
    ProcessCredentials? runAs)
{
    var debugLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-debug-{Guid.NewGuid():N}.log");
    var fullArgs = $"{args} --debug-file {EscapeArg(debugLogPath)}";

    var startInfo = new ProcessStartInfo
    {
        FileName = "claude",
        Arguments = fullArgs,
        WorkingDirectory = workingDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    startInfo.Environment["ANTHROPIC_API_KEY"] = apiToken;

    if (runAs != null)
        ApplyCredentials(startInfo, runAs);

    // ... rest of process lifecycle unchanged ...
}
```

Keep `EscapeArg` as a private method in the runner (it's still needed for the debug file path). The builder has its own copy.

Update `RunAsync`:

```csharp
public async Task<string> RunAsync(
    string args,
    string apiToken,
    IReadOnlyDictionary<string, McpServerConfig> mcpServers,
    ProcessCredentials? runAs = null)
{
    var workingDir = Path.Combine(Path.GetTempPath(), $"claude-agent-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workingDir);
    log.Verbose($"Claude Code working directory: {workingDir}");

    try
    {
        SetupSkills(workingDir);
        SetupMcpConfig(workingDir, mcpServers);
        return await RunInDirectoryAsync(args, apiToken, workingDir, runAs);
    }
    finally
    {
        try { Directory.Delete(workingDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
```

- [ ] **Step 2: Verify the AiAgent project builds** (will fail until behaviour is updated in Task 4)

Run: `dotnet build Calamari.AiAgent --no-restore -v minimal`
Expected: Build failure in `InvokeClaudeCodeBehaviour.cs` — still references `ClaudeCodeOptions`.

- [ ] **Step 3: Commit**

```bash
git add Calamari.AiAgent/Behaviours/ClaudeCodeCliRunner.cs
git commit -m "refactor: remove ClaudeCodeOptions and BuildArguments from ClaudeCodeCliRunner"
```

---

### Task 4: Update InvokeClaudeCodeBehaviour to use the builder

**Files:**
- Modify: `Calamari.AiAgent/Behaviours/InvokeClaudeCodeBehaviour.cs`

- [ ] **Step 1: Replace ClaudeCodeOptions usage with builder**

Update the `Execute` method in `InvokeClaudeCodeBehaviour.cs`:

```csharp
public async Task Execute(RunningDeployment context)
{
    var variables = context.Variables;

    var prompt = variables.Get(SpecialVariables.Action.AiAgent.Prompt);
    if (string.IsNullOrWhiteSpace(prompt))
        throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.Prompt}' is required but was not provided.");

    var apiToken = variables.Get(SpecialVariables.Action.AiAgent.ApiToken);
    if (string.IsNullOrWhiteSpace(apiToken))
        throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.ApiToken}' is required but was not provided.");

    var model = variables.Get(SpecialVariables.Action.AiAgent.Model);
    if (string.IsNullOrWhiteSpace(model))
        model = "claude-sonnet-4-20250514";

    log.Info($"Invoking Claude Code CLI with model '{model}'...");

    var mcpServers = BuildMcpServers(variables);
    var runAs = BuildRunAs(variables);

    var argsBuilder = new ClaudeCommandArgsBuilder()
        .WithPrompt(prompt)
        .WithModel(model)
        .WithAllowedTools(new[] { "Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch" });

    var systemPrompt = variables.Get(SpecialVariables.Action.AiAgent.SystemSkill);
    if (!string.IsNullOrWhiteSpace(systemPrompt))
        argsBuilder.WithSystemPrompt(systemPrompt);

    var maxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTurns);
    if (maxTurns.HasValue)
        argsBuilder.WithMaxTurns(maxTurns.Value);

    var maxBudgetUsdRaw = variables.Get(SpecialVariables.Action.AiAgent.MaxBudgetUsd);
    if (!string.IsNullOrWhiteSpace(maxBudgetUsdRaw)
        && decimal.TryParse(maxBudgetUsdRaw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var budgetUsd))
        argsBuilder.WithMaxBudgetUsd(budgetUsd);

    var args = argsBuilder.Build();

    var runner = new ClaudeCodeCliRunner(log);
    var response = await runner.RunAsync(args, apiToken, mcpServers, runAs);

    Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, response, variables);
    log.Info("Claude Code invocation complete.");
}
```

Note: `IVariables` has no `GetDecimal` method, so we parse `MaxBudgetUsd` from the string using `decimal.TryParse` with `InvariantCulture`. The builder does not handle `--mcp-config`, `--strict-mcp-config`, or `--debug-file` — the runner appends those since it owns the working directory and debug file paths.

- [ ] **Step 2: Build the full solution**

Run: `dotnet build Calamari.AiAgent --no-restore -v minimal`
Expected: PASS — all references resolved.

- [ ] **Step 3: Run all AiAgent tests**

Run: `dotnet test Calamari.AiAgent.Tests --no-restore -v minimal`
Expected: All tests pass (builder tests + remaining runner tests for SetupSkills/SetupMcpConfig).

- [ ] **Step 4: Commit**

```bash
git add Calamari.AiAgent/Behaviours/InvokeClaudeCodeBehaviour.cs Calamari.AiAgent/Behaviours/ClaudeCodeCliRunner.cs Calamari.AiAgent/Behaviours/ClaudeCommandArgsBuilder.cs Calamari.AiAgent.Tests/ClaudeCommandArgsBuilderFixture.cs
git commit -m "feat: wire up ClaudeCommandArgsBuilder in behaviour, add max-turns and max-budget-usd support"
```

---

### Task 5: Update existing test fixture

**Files:**
- Modify: `Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs`

- [ ] **Step 1: Remove tests that referenced BuildArguments and ClaudeCodeOptions**

Remove these tests from `ClaudeCodeCliRunnerFixture.cs`:
- `BuildArguments_IncludesRequiredFlags`
- `BuildArguments_IncludesAllowedTools`
- `BuildArguments_OmitsAllowedTools_WhenEmpty`
- `BuildArguments_IncludesMaxTurns_WhenSet`
- `BuildArguments_OmitsMaxTurns_WhenNotSet`
- `BuildArguments_IncludesSystemPrompt_WhenSet`
- `BuildArguments_OmitsSystemPrompt_WhenNotSet`
- `BuildArguments_EscapesPromptWithSpaces`

Remove the `DefaultOptions` helper method.

Keep:
- `SetupSkills_CreatesSkillFile`
- `SetupMcpConfig_WritesValidJson_WithServers`
- `SetupMcpConfig_WritesEmptyServers_WhenNoneProvided`

The file should look like:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.AiAgent.Behaviours;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class ClaudeCodeCliRunnerFixture
{
    [Test]
    public void SetupSkills_CreatesSkillFile()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupSkills(workingDir);

            var skillPath = Path.Combine(workingDir, ".claude", "skills", "octopus-deployment-context.md");
            File.Exists(skillPath).Should().BeTrue();

            var content = File.ReadAllText(skillPath);
            content.Should().Contain("name: octopus-deployment-context");
            content.Should().Contain("description:");
            content.Should().Contain("get_deployment_variables");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupMcpConfig_WritesValidJson_WithServers()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var servers = new Dictionary<string, McpServerConfig>
            {
                ["github"] = new McpServerConfig
                {
                    Command = "npx",
                    Args = new[] { "-y", "@modelcontextprotocol/server-github" },
                    Env = new Dictionary<string, string> { ["TOKEN"] = "abc123" },
                },
            };

            ClaudeCodeCliRunner.SetupMcpConfig(workingDir, servers);

            var configPath = Path.Combine(workingDir, "mcp-config.json");
            File.Exists(configPath).Should().BeTrue();

            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
            mcpServers.TryGetProperty("github", out var github).Should().BeTrue();
            github.GetProperty("command").GetString().Should().Be("npx");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupMcpConfig_WritesEmptyServers_WhenNoneProvided()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-empty-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupMcpConfig(workingDir, new Dictionary<string, McpServerConfig>());

            var configPath = Path.Combine(workingDir, "mcp-config.json");
            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
            mcpServers.EnumerateObject().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }
}
```

- [ ] **Step 2: Run all tests**

Run: `dotnet test Calamari.AiAgent.Tests --no-restore -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs
git commit -m "refactor: remove BuildArguments tests from runner fixture (moved to builder)"
```

