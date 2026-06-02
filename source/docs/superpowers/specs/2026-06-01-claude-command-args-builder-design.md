# Claude Command Args Builder + max-turns / max-budget-usd

## Summary

Refactor CLI argument construction out of `ClaudeCodeCliRunner` into a fluent `ClaudeCommandArgsBuilder`, and add support for two new optional step variables: `max-turns` (default 10) and `max-budget-usd` (optional).

## Motivation

- `ClaudeCodeCliRunner` is accumulating responsibilities — process lifecycle, argument building, credential handling, MCP/skill setup. Extracting arg building reduces complexity.
- Users need control over agent turn limits and spend caps.
- `MaxTokens` variable exists but was incorrectly mapped to `MaxTurns` — this is a stale bug to clean up.

## Design

### ClaudeCommandArgsBuilder (new file)

**File:** `Behaviours/ClaudeCommandArgsBuilder.cs`

Fluent builder that produces a CLI arguments string. Owns argument escaping. No knowledge of processes, environment variables, or credentials.

```csharp
public class ClaudeCommandArgsBuilder
{
    public ClaudeCommandArgsBuilder WithPrompt(string prompt) { ... }
    public ClaudeCommandArgsBuilder WithModel(string model) { ... }
    public ClaudeCommandArgsBuilder WithSystemPrompt(string systemPrompt) { ... }
    public ClaudeCommandArgsBuilder WithMaxTurns(int maxTurns) { ... }
    public ClaudeCommandArgsBuilder WithMaxBudgetUsd(decimal budgetUsd) { ... }
    public ClaudeCommandArgsBuilder WithAllowedTools(IReadOnlyList<string> tools) { ... }
    public string Build() { ... }  // returns full args string
    // Note: --mcp-config, --strict-mcp-config, and --debug-file are appended by the runner
    // since it owns working directory and debug file paths.

}
```

**Builder behaviour:**
- `WithMaxBudgetUsd` is only called when the variable is provided — method takes non-nullable `decimal`. Builder tracks whether it was called and only emits `--max-budget-usd` if so.
- `WithMaxTurns` is only called when the variable is provided — method takes non-nullable `int`. Builder internally defaults to `10` if `WithMaxTurns` was never called.
- `Build()` validates that prompt is set (required). Throws if missing.
- Argument escaping handled internally (moves `EscapeArg` into the builder).
- Emits flags: `-p`, `--model`, `--output-format stream-json`, `--verbose`, `--permission-mode dontAsk`, `--no-session-persistence`, `--allowedTools`, `--max-turns`, `--max-budget-usd`, `--system-prompt`.

### ClaudeCodeCliRunner (modified)

**File:** `Behaviours/ClaudeCodeCliRunner.cs`

- Remove `BuildArguments` static method.
- Remove `ClaudeCodeOptions` record.
- `RunAsync` takes individual parameters or a simpler signature — the behaviour constructs the args string via the builder and passes it along with the other config the runner needs (API token, credentials, MCP servers, system prompt for skill setup).
- Keep: process lifecycle, stream processing, `ApplyCredentials`, `SetupMcpConfig`, `SetupSkills`.
- Keep: `ProcessCredentials`, `McpServerConfig` records.
- Keep: `EscapeArg` can be removed from here once moved to builder (or kept as internal if runner still needs it).

### SpecialVariables (modified)

**File:** `SpecialVariables.cs`

```csharp
public const string MaxTurns = "Octopus.Action.Claude.MaxTurns";
public const string MaxBudgetUsd = "Octopus.Action.Claude.MaxBudgetUsd";
```

- Remove `MaxTokens` (stale, was incorrectly used).

### InvokeClaudeCodeBehaviour (modified)

**File:** `Behaviours/InvokeClaudeCodeBehaviour.cs`

- Read `MaxTurns` variable, default to `10` if absent.
- Read `MaxBudgetUsd` variable, only set on builder if present.
- Construct args via `ClaudeCommandArgsBuilder` fluent API.
- Pass built args string + API token + credentials + MCP servers to runner.

```csharp
var argsBuilder = new ClaudeCommandArgsBuilder()
    .WithPrompt(prompt)
    .WithModel(model)
    .WithSystemPrompt(systemPrompt)
    .WithAllowedTools(allowedTools)
    .WithMcpConfig(mcpConfigPath)
    .WithDebugFile(debugFilePath);

var maxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTurns);
if (maxTurns.HasValue)
    argsBuilder.WithMaxTurns(maxTurns.Value);

var budgetUsd = variables.GetDecimal(SpecialVariables.Action.AiAgent.MaxBudgetUsd);
if (budgetUsd.HasValue)
    argsBuilder.WithMaxBudgetUsd(budgetUsd.Value);

var args = argsBuilder.Build();  // max-turns defaults to 10 internally
```

### OctopusDeploy Step Definition (separate repo)

**File:** In `/Users/robert/Development/Octopus/OctopusDeploy/` — `AiAgentStepDocumentation.cs`

Add two new `StepPropertyInfo` entries:
- `Octopus.Action.Claude.MaxTurns` — "Maximum number of agentic turns (default: 10)"
- `Octopus.Action.Claude.MaxBudgetUsd` — "Maximum budget in USD for the agent run"

Remove `Octopus.Action.Claude.MaxTokens` if present.

## Files Changed

| File | Action |
|------|--------|
| `Behaviours/ClaudeCommandArgsBuilder.cs` | New — fluent builder |
| `Behaviours/ClaudeCodeCliRunner.cs` | Modified — remove `BuildArguments`, `ClaudeCodeOptions`, accept args string |
| `SpecialVariables.cs` | Modified — add `MaxTurns`, `MaxBudgetUsd`, remove `MaxTokens` |
| `Behaviours/InvokeClaudeCodeBehaviour.cs` | Modified — use builder, read new variables |
| OctopusDeploy `AiAgentStepDocumentation.cs` | Modified — add step properties |

## Testing

- Unit tests for `ClaudeCommandArgsBuilder`: verify each flag is emitted correctly, escaping works, optional flags omitted when not set, validation throws on missing prompt.
- Update existing `ClaudeCodeCliRunner` tests if they reference `BuildArguments` or `ClaudeCodeOptions`.
