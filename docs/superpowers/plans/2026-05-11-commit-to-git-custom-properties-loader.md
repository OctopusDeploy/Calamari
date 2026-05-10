# CommitToGit Custom Properties Loader — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move git credentials for the `commit-to-git` command out of `IVariables` and into a mandatory encrypted JSON properties file, mirroring the Argo CD encrypted-properties pattern.

**Architecture:** Add a new `CommitToGitCustomPropertiesDto` (wrapping a `NamedGitCredentialDto`) in the `Calamari.Contracts` project. Add `--customPropertiesFile` and `--customPropertiesPassword` CLI options to `CommitToGitCommand`, validate them on parse, construct a `CustomPropertiesLoader` (the existing shared loader at `source/Calamari.Common/Plumbing/Variables/CustomPropertiesLoader.cs`), and pass it into `CommitToGitConfigFactory.CreateRepositoryConfig`. The factory loads the DTO and uses `Credential.Username` / `Credential.Password` to build the `GitConnection`; URL/reference/commit metadata continue to come from `IVariables`. The file is mandatory — missing options or unreadable files surface as `CommandException` from the command.

**Tech Stack:** .NET 8 / C# 12, NUnit 3, NSubstitute, FluentAssertions. Build via `dotnet build`; tests via `dotnet test`.

**Spec:** `docs/superpowers/specs/2026-05-11-commit-to-git-custom-properties-loader-design.md`

---

## Pre-flight

- [ ] **Step 0a: Resolve detached HEAD before any commits**

The repo is currently on a detached HEAD (`a6b0fc762` holds the spec commit). Decide with the user whether to attach the spec commit to a branch (e.g. `git switch -c tmm/commit-to-git-properties-loader a6b0fc762`) or to reset to `origin/main` and re-apply later. **Do not start Task 1 until this is resolved** — otherwise every commit in this plan will land on the same dangling HEAD.

- [ ] **Step 0b: Verify the baseline test suite passes before changing anything**

Run:
```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CommitToGit" --nologo
```
Expected: every `CommitToGit*` test passes. Record the count so we can confirm the new plan does not silently drop tests.

---

## Task 1: Add `CommitToGitCustomPropertiesDto` to `Calamari.Contracts`

**Files:**
- Create: `source/Calamari.Contracts/CommitToGit/CommitToGitCustomPropertiesDto.cs`

These are plain DTOs with no behaviour to test, so this task is "write the types, compile, commit." No unit tests on the DTO itself — they are exercised via the factory tests in Task 2.

- [ ] **Step 1: Create the new DTO file**

Create `source/Calamari.Contracts/CommitToGit/CommitToGitCustomPropertiesDto.cs`:

```csharp
namespace Octopus.Calamari.Contracts.CommitToGit;

public record CommitToGitCustomPropertiesDto(NamedGitCredentialDto Credential);

public record NamedGitCredentialDto(string Username, string Password, string Name);
```

Notes for the implementer:
- The folder `source/Calamari.Contracts/CommitToGit/` does not exist yet — `dotnet`'s SDK-style projects pick up `.cs` files automatically, so no `.csproj` edit is required. Confirm by listing the folder after creating the file.
- Mirror the Argo file's style: file-scoped namespace, no `using`s required.

- [ ] **Step 2: Build to confirm the types compile**

Run:
```bash
dotnet build source/Calamari.Contracts/Calamari.Contracts.csproj --nologo
```
Expected: `0 Error(s)`. Warnings about other projects can be ignored.

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.Contracts/CommitToGit/CommitToGitCustomPropertiesDto.cs
git commit -m "add CommitToGitCustomPropertiesDto contract for encrypted git credential payload"
```

---

## Task 2: Factory loads credential from the properties loader (happy path)

**Files:**
- Create: `source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs`
- Modify: `source/Calamari/CommitToGit/CommitToGitConfigFactory.cs`

This task introduces the new factory signature via TDD. We start with the success path; the null-`Credential` guard lands in Task 3, and the command-level wiring in Task 4.

- [ ] **Step 1: Write the failing happy-path test**

Create `source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs`:

```csharp
using System;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.CommitToGit;
using Calamari.Deployment;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.CommitToGit;

namespace Calamari.Tests.CommitToGit;

[TestFixture]
public class CommitToGitConfigFactoryTests
{
    INonSensitiveVariables nonSensitiveVariables;
    IVariables variables;
    ICustomPropertiesLoader loader;
    CommitToGitConfigFactory factory;

    [SetUp]
    public void SetUp()
    {
        nonSensitiveVariables = Substitute.For<INonSensitiveVariables>();
        variables = Substitute.For<IVariables>();
        loader = Substitute.For<ICustomPropertiesLoader>();
        factory = new CommitToGitConfigFactory(nonSensitiveVariables);

        // Minimum required variables for CreateRepositoryConfig to succeed:
        variables.Get(SpecialVariables.Action.Git.Url).Returns("https://example.invalid/repo.git");
        variables.Get(SpecialVariables.Action.Git.Reference).Returns("refs/heads/main");
        nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Action.Git.CommitMessageSummary)
                             .Returns("summary");
        nonSensitiveVariables.GetRaw(SpecialVariables.Action.Git.CommitMessageDescription)
                             .Returns(string.Empty);
        nonSensitiveVariables.Evaluate(Arg.Any<string>(), out Arg.Any<string>())
                             .Returns(ci =>
                             {
                                 ci[1] = null;
                                 return (string)ci[0];
                             });
    }

    [Test]
    public void CreateRepositoryConfig_UsesUsernameAndPasswordFromLoadedProperties()
    {
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto(
                           new NamedGitCredentialDto("user-from-file", "pwd-from-file", "MyCred")));

        var deployment = new RunningDeployment(null, variables);

        var config = factory.CreateRepositoryConfig(deployment, loader);

        config.GitConnection.Username.Should().Be("user-from-file");
        config.GitConnection.Password.Should().Be("pwd-from-file");
        config.GitConnection.Url.Should().Be(new Uri("https://example.invalid/repo.git"));
    }
}
```

Notes:
- `Calamari.ArgoCD.Git` is the namespace for `GitConnection`/`IGitConnection` — verify by reading `source/Calamari/ArgoCD/Git/GitConnection.cs` if anything moved.
- The `Evaluate` stub returns the expression unchanged with `error=null`; this matches what `EvaluateNonsensitiveExpression` does on the happy path.

- [ ] **Step 2: Run the test and confirm it fails**

Run:
```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGitConfigFactoryTests" --nologo
```
Expected: compile error — `CreateRepositoryConfig` does not accept an `ICustomPropertiesLoader`. That counts as a failing test for TDD purposes; do not skip ahead.

- [ ] **Step 3: Update `CommitToGitConfigFactory.CreateRepositoryConfig` to accept the loader and use it**

Modify `source/Calamari/CommitToGit/CommitToGitConfigFactory.cs`:

1. Add `using Calamari.Common.Plumbing.FileSystem;` (only if not already present — needed for nothing here, ignore if absent).
2. Add `using Octopus.Calamari.Contracts.CommitToGit;` at the top.
3. Change the signature and replace the two variable reads:

```csharp
public CommitToGitRepositorySettings CreateRepositoryConfig(
    RunningDeployment deployment,
    ICustomPropertiesLoader customPropertiesLoader)
{
    var variables = deployment.Variables;

    var uriAsString = variables.Get(SpecialVariables.Action.Git.Url)
        ?? throw new CommandException($"Required variable '{SpecialVariables.Action.Git.Url}' is not set.");

    var gitReferenceAsString = variables.Get(SpecialVariables.Action.Git.Reference)
        ?? throw new CommandException($"Required variable '{SpecialVariables.Action.Git.Reference}' is not set.");

    var requiresPullRequest = variables.GetFlag(SpecialVariables.Action.Git.PullRequest.Create);
    var summary = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Action.Git.CommitMessageSummary));
    var description = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetRaw(SpecialVariables.Action.Git.CommitMessageDescription) ?? string.Empty);
    var commitParameters = new GitCommitParameters(summary, description, requiresPullRequest);

    var properties = customPropertiesLoader.Load<CommitToGitCustomPropertiesDto>();
    var credential = properties.Credential;

    return new CommitToGitRepositorySettings(
        new GitConnection(
            credential.Username,
            credential.Password,
            new Uri(uriAsString),
            GitReference.CreateFromString(gitReferenceAsString)),
        commitParameters,
        variables.Get(SpecialVariables.Action.Git.DestinationPath));
}
```

Delete (or ignore — the compiler will not complain either way) the previous calls to `variables.Get(SpecialVariables.Action.Git.Username)` and `variables.Get(SpecialVariables.Action.Git.Password)`. They no longer have any callers within this method.

- [ ] **Step 4: Re-run the factory test**

Run:
```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGitConfigFactoryTests" --nologo
```
Expected: 1 test passing. The command-level tests (`CommitToGitCommandTest`) will now be broken because they call the old `CreateRepositoryConfig(deployment)` signature through `Program.Main`; that is fixed in Task 4. Do not run them yet.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs \
        source/Calamari/CommitToGit/CommitToGitConfigFactory.cs
git commit -m "load CommitToGit git credential via CustomPropertiesLoader instead of variables"
```

---

## Task 3: Factory guards against a null `Credential`

**Files:**
- Modify: `source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs`
- Modify: `source/Calamari/CommitToGit/CommitToGitConfigFactory.cs`

The DTO is deserialized from JSON; a malformed payload could leave `Credential` null. We surface that as a clean `CommandException`, not a `NullReferenceException`.

- [ ] **Step 1: Write the failing null-credential test**

Append to `CommitToGitConfigFactoryTests`:

```csharp
[Test]
public void CreateRepositoryConfig_ThrowsCommandException_WhenLoadedCredentialIsNull()
{
    loader.Load<CommitToGitCustomPropertiesDto>()
          .Returns(new CommitToGitCustomPropertiesDto(null));

    var deployment = new RunningDeployment(null, variables);

    var act = () => factory.CreateRepositoryConfig(deployment, loader);

    act.Should().Throw<CommandException>()
       .WithMessage("*git credential*");
}
```

- [ ] **Step 2: Run and confirm it fails with `NullReferenceException`**

Run:
```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CreateRepositoryConfig_ThrowsCommandException_WhenLoadedCredentialIsNull" --nologo
```
Expected: 1 test failing — `NullReferenceException` thrown instead of `CommandException`.

- [ ] **Step 3: Add the guard in the factory**

In `CreateRepositoryConfig`, immediately after `var credential = properties.Credential;`:

```csharp
if (credential == null)
    throw new CommandException("Custom properties file did not contain a git credential.");
```

- [ ] **Step 4: Re-run the test**

Run the same `dotnet test` command. Expected: passing.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs \
        source/Calamari/CommitToGit/CommitToGitConfigFactory.cs
git commit -m "fail clearly when CommitToGit custom properties contains no credential"
```

---

## Task 4: Wire CLI options + loader into `CommitToGitCommand`

**Files:**
- Modify: `source/Calamari/Commands/CommitToGitCommand.cs`
- Modify: `source/Calamari.Tests/CommitToGitCommandTest.cs`

This task adds the two CLI options, validates them, constructs a `CustomPropertiesLoader`, hands it to the factory, and updates the existing command-level tests so they continue to drive the command end-to-end.

The reviewer recommendation is that file-not-found surfaces at the call site (the command), not inside the shared `CustomPropertiesLoader`. We do this by checking `fileSystem.FileExists(...)` before constructing the loader. This keeps the loader contract identical for Argo.

- [ ] **Step 1: Write the failing test — missing `--customPropertiesFile`**

In `source/Calamari.Tests/CommitToGitCommandTest.cs`, append (alongside the existing tests):

```csharp
[Test]
public void CommitToGit_FailsWithCommandException_WhenCustomPropertiesFileOptionIsMissing()
{
    var exitCode = RunCommitToGit(includeCustomProperties: false);

    exitCode.Should().NotBe(0,
        "the command must reject runs that do not supply --customPropertiesFile");
}
```

This relies on a new overload of `RunCommitToGit` introduced in Step 3.

- [ ] **Step 2: Confirm it fails**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGit_FailsWithCommandException_WhenCustomPropertiesFileOptionIsMissing" --nologo
```
Expected: compile error because the new overload does not exist yet. (Acceptable as the "failing" state.)

- [ ] **Step 3: Update the test helper to write an encrypted properties file**

Replace the existing `RunCommitToGit` helper and the `setUp` block in `CommitToGitCommandTest.cs`:

a. In `setUp`, remove these two lines (they are no longer read):

```csharp
new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Username, "", false),
new CalamariExecutionVariable(Deployment.SpecialVariables.Action.Git.Password, "", true),
```

b. Add a field at the top of the class:

```csharp
readonly string customPropertiesPassword = "props-password";
readonly string customPropertiesFileName = "custom-properties.json";
```

c. Replace `RunCommitToGit` with:

```csharp
int RunCommitToGit(params string[] extraArgs)
    => RunCommitToGit(includeCustomProperties: true, extraArgs);

int RunCommitToGit(bool includeCustomProperties, params string[] extraArgs)
{
    var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
    File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

    var args = new List<string>
    {
        "commit-to-git",
        "--variables", absPathToVariables,
        "--variablesPassword", variablePassword,
    };

    if (includeCustomProperties)
    {
        var propsPath = WriteCustomPropertiesFile("git-user", "git-password", "MyCred");
        args.AddRange(["--customPropertiesFile", propsPath, "--customPropertiesPassword", customPropertiesPassword]);
    }

    args.AddRange(extraArgs);

    return Program.Main(args.ToArray());
}

string WriteCustomPropertiesFile(string username, string password, string name)
{
    var dto = new CommitToGitCustomPropertiesDto(new NamedGitCredentialDto(username, password, name));
    var json = JsonConvert.SerializeObject(dto);
    var absPath = Path.Combine(executionDirectory, customPropertiesFileName);
    File.WriteAllBytes(absPath, AesEncryption.ForServerVariables(customPropertiesPassword).Encrypt(json));
    return absPath;
}
```

d. Add to the using block at the top of the test file:

```csharp
using Newtonsoft.Json;
using Octopus.Calamari.Contracts.CommitToGit;
```

- [ ] **Step 4: Add the failing `--customPropertiesPassword`-missing test**

```csharp
[Test]
public void CommitToGit_FailsWithCommandException_WhenCustomPropertiesPasswordOptionIsMissing()
{
    var propsPath = WriteCustomPropertiesFile("u", "p", "n");

    var exitCode = RunCommitToGit(includeCustomProperties: false,
                                  "--customPropertiesFile", propsPath);

    exitCode.Should().NotBe(0,
        "the command must reject runs that do not supply --customPropertiesPassword");
}
```

- [ ] **Step 5: Add the failing file-not-found test**

```csharp
[Test]
public void CommitToGit_FailsWithCommandException_WhenCustomPropertiesFileDoesNotExist()
{
    var missingPath = Path.Combine(executionDirectory, "does-not-exist.json");

    var exitCode = RunCommitToGit(includeCustomProperties: false,
                                  "--customPropertiesFile", missingPath,
                                  "--customPropertiesPassword", customPropertiesPassword);

    exitCode.Should().NotBe(0,
        "the command must reject runs whose --customPropertiesFile path does not exist");
}
```

- [ ] **Step 6: Run all three new tests, confirm they fail or error out**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGit_FailsWithCommandException_" --nologo
```
Expected: all three fail (likely as exceptions thrown out of `Program.Main`, or simply non-zero exit). Either failure mode is fine — we are about to change the code to make them pass cleanly.

- [ ] **Step 7: Add options + validation + loader construction to `CommitToGitCommand`**

In `source/Calamari/Commands/CommitToGitCommand.cs`:

a. Add fields next to the other option-target fields:

```csharp
string customPropertiesFile;
string customPropertiesPassword;
```

b. In the constructor, register the new options (next to the existing `Options.Add` calls):

```csharp
Options.Add("customPropertiesFile=",
            "Path to an encrypted JSON file containing the git credential.",
            v => customPropertiesFile = Path.GetFullPath(v));
Options.Add("customPropertiesPassword=",
            "Password to decrypt the custom properties file.",
            v => customPropertiesPassword = v);
```

c. In `Execute`, immediately after `ApplyScriptParametersOverride();`, validate and construct the loader:

```csharp
if (!WasProvided(customPropertiesFile))
    throw new CommandException("Required option --customPropertiesFile was not provided.");
if (!WasProvided(customPropertiesPassword))
    throw new CommandException("Required option --customPropertiesPassword was not provided.");
if (!fileSystem.FileExists(customPropertiesFile))
    throw new CommandException($"Custom properties file '{customPropertiesFile}' does not exist.");

var customPropertiesLoader = new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword);
```

d. Change the line that constructs the repository config from:

```csharp
var repositoryConfig = configFactory.CreateRepositoryConfig(deployment);
```

to:

```csharp
var repositoryConfig = configFactory.CreateRepositoryConfig(deployment, customPropertiesLoader);
```

e. Add a `using Calamari.Common.Plumbing.Variables;` to the file if it isn't already imported (it likely is — verify before adding).

- [ ] **Step 8: Run the three new command tests, confirm they pass**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGit_FailsWithCommandException_" --nologo
```
Expected: 3 passing.

- [ ] **Step 9: Run the full `CommitToGit` test set to confirm no regressions**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "FullyQualifiedName~CommitToGit" --nologo
```
Expected: the same count from Step 0b, plus the 4 new tests added in Tasks 2/3/4. No failures.

- [ ] **Step 10: Commit**

```bash
git add source/Calamari/Commands/CommitToGitCommand.cs \
        source/Calamari.Tests/CommitToGitCommandTest.cs
git commit -m "require --customPropertiesFile/--customPropertiesPassword in commit-to-git"
```

---

## Task 5: Confirm the broader build is clean

**Files:** none

Belt-and-braces final check. Catches accidental breakage outside the `CommitToGit*` test surface.

- [ ] **Step 1: Build the full Calamari project**

```bash
dotnet build source/Calamari/Calamari.csproj --nologo -v q 2>&1 | tail -5
```
Expected: `0 Error(s)`. The pre-existing MSB3277 / SYSLIB warnings are unrelated and can be ignored.

- [ ] **Step 2: Run the cross-platform test bucket**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj \
  --filter "TestCategory=PlatformAgnostic" --nologo
```
Expected: passing. Any unrelated failures should be diagnosed (likely environmental — e.g. missing 1Password secrets — and not caused by this work). Surface any that look related to credential handling to the user before declaring success.

- [ ] **Step 3: Tell the user the plan is complete**

Report which commits were created on the working branch, and remind them that Argo command validation was deliberately not tightened in this PR (per the spec's non-goals).

---

## Things explicitly out of scope

- Adding presence validation for `--customPropertiesFile` / `--customPropertiesPassword` to the Argo commands. Argo's current looser behaviour is preserved; the deliberate divergence should be noted in the PR description.
- Modifying the shared `CustomPropertiesLoader` to wrap `FileNotFoundException`. We handle file existence at the call site so Argo's behaviour stays untouched.
- Migrating Argo's `GitCredentialDto` to include `Name`. The two DTOs are intentionally distinct.
- Supporting multiple credentials in one CommitToGit payload.
- Making Calamari actually use the `Name` field.
