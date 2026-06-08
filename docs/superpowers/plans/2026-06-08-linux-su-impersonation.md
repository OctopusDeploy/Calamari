# Linux User Impersonation via `script`/`su` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ProcessStartInfo.UserName` with `script`/`su` for Linux user impersonation, keeping Windows behaviour unchanged.

**Architecture:** On Linux, `ApplyCredentials` rewrites the `ProcessStartInfo` to launch `script -qec "su - {user} -c '{envVars} {cmd}'" /dev/null` and pipes the password via stdin. Environment variables explicitly added to `startInfo.Environment` are inlined into the `su -c` command string (since `su -` starts a login shell that clears inherited env). `RunProcess` gains a password parameter to write to stdin. Windows path is untouched.

**Tech Stack:** C# / .NET, `System.Diagnostics.Process`, `System.Runtime.InteropServices.RuntimeInformation`

---

### Task 1: Track explicitly-set environment variables

Currently `ANTHROPIC_API_KEY` is set directly on `startInfo.Environment`, which inherits the full parent environment. We need to know which keys were *explicitly added* so we can inline only those into the `su -c` command.

**Files:**
- Modify: `source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs:89-110`

- [ ] **Step 1: Introduce a list to track custom env vars in `RunInDirectoryAsync`**

Change the environment setup from:

```csharp
startInfo.Environment["ANTHROPIC_API_KEY"] = apiToken;
```

To:

```csharp
var customEnvVars = new Dictionary<string, string>
{
    ["ANTHROPIC_API_KEY"] = apiToken,
};

foreach (var kvp in customEnvVars)
    startInfo.Environment[kvp.Key] = kvp.Value;
```

This is a no-op refactor — behaviour is identical. The `customEnvVars` dictionary will be passed to `ApplyCredentials` in the next task.

- [ ] **Step 2: Run existing tests to confirm no regression**

Run: `dotnet test source/Calamari.AiAgent.Tests/ --filter "FullyQualifiedName~ClaudeCodeCliRunnerFixture" --no-build -v quiet`
Expected: All tests pass (these test static helpers, not process launch)

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs
git commit -m "refactor: track explicitly-set env vars in RunInDirectoryAsync"
```

---

### Task 2: Add shell-quoting helper

The `su -c` command requires values to be safely quoted to avoid injection. We need a helper that single-quotes a string, escaping any embedded single quotes.

**Files:**
- Modify: `source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs` (add new static method)
- Test: `source/Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs`

- [ ] **Step 1: Write failing tests for ShellQuote**

Add to `ClaudeCodeCliRunnerFixture.cs`:

```csharp
[TestCase("simple", "'simple'")]
[TestCase("has space", "'has space'")]
[TestCase("it's", @"'it'\''s'")]
[TestCase("", "''")]
[TestCase("a'b'c", @"'a'\''b'\''c'")]
public void ShellQuote_QuotesCorrectly(string input, string expected)
{
    ClaudeCodeCliRunner.ShellQuote(input).Should().Be(expected);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test source/Calamari.AiAgent.Tests/ --filter "FullyQualifiedName~ShellQuote" -v quiet`
Expected: FAIL — `ShellQuote` does not exist

- [ ] **Step 3: Implement ShellQuote**

Add to `ClaudeCodeCliRunner`:

```csharp
internal static string ShellQuote(string value)
{
    return "'" + value.Replace("'", @"'\''") + "'";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test source/Calamari.AiAgent.Tests/ --filter "FullyQualifiedName~ShellQuote" -v quiet`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs source/Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs
git commit -m "feat: add ShellQuote helper for safe single-quoting in su commands"
```

---

### Task 3: Rewrite `ApplyCredentials` with Linux `script`/`su` path

**Files:**
- Modify: `source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs:277-294`
- Test: `source/Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs`

- [ ] **Step 1: Write failing tests for Linux ApplyCredentials**

Add to `ClaudeCodeCliRunnerFixture.cs`:

```csharp
[Test]
public void ApplyCredentials_Linux_RewritesStartInfoToUseScriptSu()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Assert.Ignore("Linux-only test");
        return;
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = "claude",
        Arguments = "--model sonnet --print",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    var credentials = new ProcessCredentials
    {
        Username = "claude",
        Password = "claude",
    };

    var customEnvVars = new Dictionary<string, string>
    {
        ["ANTHROPIC_API_KEY"] = "sk-test-123",
        ["OTHER_VAR"] = "hello",
    };

    ClaudeCodeCliRunner.ApplyCredentials(startInfo, credentials, customEnvVars);

    startInfo.FileName.Should().Be("script");
    startInfo.UserName.Should().BeNull();
    startInfo.RedirectStandardInput.Should().BeTrue();

    // ArgumentList should be: -qec, "su - claude -c '...'", /dev/null
    startInfo.ArgumentList.Should().HaveCount(3);
    startInfo.ArgumentList[0].Should().Be("-qec");
    startInfo.ArgumentList[2].Should().Be("/dev/null");

    var suCommand = startInfo.ArgumentList[1];
    suCommand.Should().StartWith("su - claude -c ");
    suCommand.Should().Contain("ANTHROPIC_API_KEY=");
    suCommand.Should().Contain("OTHER_VAR=");
    suCommand.Should().Contain("claude --model sonnet --print");
}

[Test]
public void ApplyCredentials_Linux_ThrowsWhenPasswordMissing()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Assert.Ignore("Linux-only test");
        return;
    }

    var startInfo = new ProcessStartInfo { FileName = "claude" };
    var credentials = new ProcessCredentials { Username = "claude", Password = null };
    var customEnvVars = new Dictionary<string, string>();

    var act = () => ClaudeCodeCliRunner.ApplyCredentials(startInfo, credentials, customEnvVars);

    act.Should().Throw<CommandException>().WithMessage("*password*");
}

[Test]
public void ApplyCredentials_Windows_SetsUsernameAndPassword()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Assert.Ignore("Windows-only test");
        return;
    }

    var startInfo = new ProcessStartInfo { FileName = "claude" };
    var credentials = new ProcessCredentials
    {
        Username = "deploy-user",
        Password = "s3cret",
        Domain = "CORP",
    };
    var customEnvVars = new Dictionary<string, string>();

    ClaudeCodeCliRunner.ApplyCredentials(startInfo, credentials, customEnvVars);

    startInfo.UserName.Should().Be("deploy-user");
    startInfo.PasswordInClearText.Should().Be("s3cret");
    startInfo.Domain.Should().Be("CORP");
    startInfo.FileName.Should().Be("claude"); // unchanged
}
```

Also add these using statements at the top of the test file if not already present:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test source/Calamari.AiAgent.Tests/ --filter "FullyQualifiedName~ApplyCredentials" -v quiet`
Expected: FAIL — signature mismatch (new `customEnvVars` parameter)

- [ ] **Step 3: Implement the new ApplyCredentials**

Replace `ApplyCredentials` in `ClaudeCodeCliRunner.cs`:

```csharp
internal static void ApplyCredentials(ProcessStartInfo startInfo, ProcessCredentials credentials, Dictionary<string, string> customEnvVars)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        startInfo.UserName = credentials.Username;

        if (!string.IsNullOrEmpty(credentials.Password))
            startInfo.PasswordInClearText = credentials.Password;
        if (!string.IsNullOrEmpty(credentials.Domain))
            startInfo.Domain = credentials.Domain;

        return;
    }

    // Linux: use script/su to impersonate the user with a proper login shell.
    // su - starts a login shell which clears the environment, so we inline
    // any custom env vars into the command string.
    if (string.IsNullOrEmpty(credentials.Password))
        throw new CommandException("A password is required for Linux user impersonation via su");

    var envPrefix = string.Join(" ", customEnvVars.Select(kvp => $"{kvp.Key}={ShellQuote(kvp.Value)}"));
    var innerCommand = string.IsNullOrEmpty(envPrefix)
        ? $"{startInfo.FileName} {startInfo.Arguments}"
        : $"{envPrefix} {startInfo.FileName} {startInfo.Arguments}";

    var suCommand = $"su - {credentials.Username} -c {ShellQuote(innerCommand)}";

    startInfo.FileName = "script";
    startInfo.Arguments = ""; // clear — using ArgumentList instead
    startInfo.ArgumentList.Add("-qec");
    startInfo.ArgumentList.Add(suCommand);
    startInfo.ArgumentList.Add("/dev/null");
    startInfo.RedirectStandardInput = true;
    startInfo.UserName = null;
}
```

- [ ] **Step 4: Update the call site in RunInDirectoryAsync**

Change the call from:

```csharp
if (runAs != null)
    ApplyCredentials(startInfo, runAs);
```

To:

```csharp
if (runAs != null)
    ApplyCredentials(startInfo, runAs, customEnvVars);
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test source/Calamari.AiAgent.Tests/ --filter "FullyQualifiedName~ClaudeCodeCliRunnerFixture" -v quiet`
Expected: All pass (platform-guarded tests will run on the current OS, others will be ignored)

- [ ] **Step 6: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs source/Calamari.AiAgent.Tests/ClaudeCodeCliRunnerFixture.cs
git commit -m "feat: use script/su for Linux user impersonation instead of ProcessStartInfo.UserName"
```

---

### Task 4: Pipe password to stdin in `RunProcess`

`RunProcess` needs to write the password to stdin when running under `script`/`su` on Linux.

**Files:**
- Modify: `source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs:117,138-170`

- [ ] **Step 1: Add password parameter to RunProcess**

Change the signature from:

```csharp
async Task RunProcess(ProcessStartInfo startInfo, string verboseLogPath, ClaudeCodeStreamProcessor streamProcessor)
```

To:

```csharp
async Task RunProcess(ProcessStartInfo startInfo, string verboseLogPath, ClaudeCodeStreamProcessor streamProcessor, string? password = null)
```

- [ ] **Step 2: Add stdin writing after process.Start()**

Add immediately after `process.Start();`:

```csharp
if (password != null)
{
    await process.StandardInput.WriteLineAsync(password);
    process.StandardInput.Close();
}
```

- [ ] **Step 3: Update the call site in RunInDirectoryAsync**

Change the call from:

```csharp
await RunProcess(startInfo, verboseLogPath, streamProcessor);
```

To:

```csharp
var password = runAs != null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? runAs.Password : null;
await RunProcess(startInfo, verboseLogPath, streamProcessor, password);
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test source/Calamari.AiAgent.Tests/ -v quiet`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs
git commit -m "feat: pipe password to stdin for Linux su-based impersonation"
```

---

### Task 5: Update ADR comment

The code references an ADR about using `ProcessStartInfo.UserName` on all platforms. The comment should reflect the new approach.

**Files:**
- Modify: `source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs:277-284`

- [ ] **Step 1: Update the comment block in ApplyCredentials**

Replace the existing comment with:

```csharp
// See ADR: https://github.com/OctopusDeploy/adr/blob/main/team-modern-deployments/calamari-ai-agent/adr-001-use-processstartinfo-username-for-user-impersonation.md
// On Windows: uses ProcessStartInfo.UserName with native token-based impersonation
//   and optional password/domain.
// On Linux: uses script(1) + su(1) to launch a login shell as the target user.
//   Environment variables are inlined into the su -c command since login shells
//   clear the inherited environment. Password is piped via stdin.
```

- [ ] **Step 2: Commit**

```bash
git add source/Calamari.AiAgent/ClaudeCodeBehaviour/ClaudeCodeCliRunner.cs
git commit -m "docs: update ApplyCredentials comment to reflect Linux script/su approach"
```
