# Commit-to-Git Repository Conventions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fill the empty `repositoryOperations` and `commitToRemote` convention groups in `CommitToGitCommand` by cloning the target git repository, copying staged input files into it, and committing/pushing changes — with the transform script able to modify the repo in between.

**Architecture:** The `RepositoryWrapper` is created as a captured local variable in `CommitToGitCommand.Execute()` (matching the existing pattern for `baseWorkingDirectory`, `inputsDirectory`, etc.) and passed to delegates via closure. `RepositoryFactory` is instantiated manually inside the clone delegate (following the same pattern as `UpdateArgoCDAppManifestsCommand`). A new `StageAllChanges()` method on `RepositoryWrapper` wraps LibGit2Sharp's `Commands.Stage("*")` to handle new, modified, and deleted files in one call.

**Tech Stack:** C# / .NET 8, LibGit2Sharp, `ICalamariFileSystem`, Autofac DI, NUnit 3

**Spec:** `docs/superpowers/specs/2026-04-13-commit-to-git-repository-conventions-design.md`

---

## File Map

**Modify:**
- `source/Calamari/ArgoCD/Git/RepositoryWrapper.cs` — add `StageAllChanges()` method
- `source/Calamari/ArgoCD/Conventions/DeploymentConfigFactory.cs` — make `CommitParameters` public
- `source/Calamari/Commands/CommitToGitCommand.cs` — add constructor param, wire delegates, dispose, fix duplicate bug

**Delete:**
- `source/Calamari/Deployment/Conventions/GitRepositoryConvention.cs` — replaced by inline delegates

---

## Task 1: Delete stale `GitRepositoryConvention.cs`

This skeleton is superseded by the inline delegate approach. It doesn't compile cleanly and must be removed first.

**Files:**
- Delete: `source/Calamari/Deployment/Conventions/GitRepositoryConvention.cs`

- [ ] **Delete the file**

```bash
git rm source/Calamari/Deployment/Conventions/GitRepositoryConvention.cs
```

- [ ] **Build to confirm no remaining references**

```bash
dotnet build source/Calamari/Calamari.csproj
```

Expected: build succeeds (the file had no callers).

- [ ] **Commit**

```bash
git commit -m "chore: remove stale GitRepositoryConvention skeleton"
```

---

## Task 2: Add `StageAllChanges()` to `RepositoryWrapper`

**Files:**
- Modify: `source/Calamari/ArgoCD/Git/RepositoryWrapper.cs`

`RepositoryWrapper` exposes `AddFiles` and `RemoveFiles` individually but has no method to stage all changes at once. The transform script may delete files from the repository, so a `git add -A` equivalent is needed.

- [ ] **Add the method after `RemoveFiles` (around line 91)**

In `source/Calamari/ArgoCD/Git/RepositoryWrapper.cs`, add after the `RemoveFiles` method:

```csharp
public void StageAllChanges()
{
    try
    {
        LibGit2Sharp.Commands.Stage(repository, "*");
    }
    catch (LibGit2SharpException e)
    {
        throw new CommandException($"Failed to stage changes in git repository. Error: {e.Message}", e);
    }
}
```

`LibGit2Sharp.Commands.Stage(repository, "*")` is equivalent to `git add -A` — it stages new files, modified files, and removes index entries for deleted files.

- [ ] **Build to confirm it compiles**

```bash
dotnet build source/Calamari/Calamari.csproj
```

Expected: build succeeds.

- [ ] **Commit**

```bash
git add source/Calamari/ArgoCD/Git/RepositoryWrapper.cs
git commit -m "feat: add StageAllChanges to RepositoryWrapper"
```

---

## ~~Task 3: Make `CommitParameters` public~~ — REMOVED

`CommitToGitRepositorySettings` (returned by `CreateCommitToGitRepositoryConfig`) already exposes `CommitParameters` as a property. The commit delegate uses `repositoryConfig.CommitParameters` instead of calling `configFactory.CommitParameters` separately. No change to `DeploymentConfigFactory` needed.

---

## Task 4: Wire up `CommitToGitCommand`

**Files:**
- Modify: `source/Calamari/Commands/CommitToGitCommand.cs`

This task fills the three empty parts of the command: the clone delegate, the copy delegate, and the commit/push delegate. It also fixes a duplicate convention list on line 149 and wraps `RunConventions()` in a `try/finally` to guarantee `RepositoryWrapper` disposal.

### Context on dependencies

- `DeploymentConfigFactory` — already registered in Autofac via `ArgoCDModule` (loaded by `Program.cs`). Add it as a constructor parameter.
- `RepositoryFactory` — **not** registered in Autofac; instantiate manually inside the clone delegate (same pattern used by `UpdateArgoCDAppManifestsCommand`).
- `IGitVendorPullRequestClientResolver` — already a constructor parameter on `CommitToGitCommand`; pass it to `RepositoryFactory`.
- `IClock` — not registered in Autofac; instantiate as `new SystemClock()` inside `Execute()` (same pattern as `UpdateArgoCDAppManifestsCommand` line 77).

### Step 1 — Add usings

- [ ] **Add the required using directives at the top of `CommitToGitCommand.cs`**

Add these three usings alongside the existing ones:

```csharp
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.Integration.Time;
```

(`System.Threading` is needed for `CancellationToken`. `Calamari.ArgoCD.Git` for `RepositoryFactory`. `Calamari.ArgoCD.Conventions` for `DeploymentConfigFactory`. `Calamari.Integration.Time` for `SystemClock`.)

### Step 2 — Add `DeploymentConfigFactory` constructor parameter

- [ ] **Add `DeploymentConfigFactory configFactory` to the constructor**

Change the constructor signature from:

```csharp
public CommitToGitCommand(ILog log, INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles, ISubstituteInFiles substituteInFiles, IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
                          ICalamariFileSystem fileSystem,
                          IVariables variables,
                          ICommandLineRunner commandLineRunner,
                          IScriptEngine scriptEngine,
                          IDeploymentJournalWriter deploymentJournalWriter)
```

To:

```csharp
public CommitToGitCommand(ILog log, INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles, ISubstituteInFiles substituteInFiles, IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
                          ICalamariFileSystem fileSystem,
                          IVariables variables,
                          ICommandLineRunner commandLineRunner,
                          IScriptEngine scriptEngine,
                          IDeploymentJournalWriter deploymentJournalWriter,
                          DeploymentConfigFactory configFactory)
```

Add the corresponding field and assignment:

```csharp
// Add field alongside the other readonly fields:
readonly DeploymentConfigFactory configFactory;

// Add assignment inside the constructor body:
this.configFactory = configFactory;
```

### Step 3 — Declare captured locals and `RepositoryWrapper`

- [ ] **Add `clonedRepository` and `clock` to `Execute()`**

In `Execute()`, add declarations alongside the existing `baseWorkingDirectory`, `transformsDirectory`, `inputsDirectory`:

```csharp
string baseWorkingDirectory = "";
string transformsDirectory = "";
string inputsDirectory = "";
RepositoryWrapper? clonedRepository = null;
CommitToGitRepositorySettings? repositoryConfig = null;
var clock = new SystemClock();
```

### Step 4 — Fill `repositoryOperations`

- [ ] **Replace the empty `repositoryOperations` list with clone and copy delegates**

Replace:

```csharp
// Create a repository and copy the inputs into the repository
var repositoryOperations = new List<IConvention>
{

};
```

With:

```csharp
// Clone the target git repository, then copy staged input files into it
var repositoryOperations = new List<IConvention>
{
    new DelegateInstallConvention(d =>
    {
        repositoryConfig = configFactory.CreateCommitToGitRepositoryConfig(d);
        var repositoryFactory = new RepositoryFactory(log, fileSystem, baseWorkingDirectory, gitVendorPullRequestClientResolver, clock);
        clonedRepository = repositoryFactory.CloneRepository("git_repository", repositoryConfig.gitConnection);
    }),
    new DelegateInstallConvention(d =>
    {
        var destinationPath = d.Variables.Get(SpecialVariables.GitRepositoryTarget.DestinationPath) ?? string.Empty;
        var destBase = Path.Combine(clonedRepository!.WorkingDirectory, destinationPath);
        foreach (var sourceFile in fileSystem.EnumerateFilesRecursively(inputsDirectory))
        {
            var relativePath = Path.GetRelativePath(inputsDirectory, sourceFile);
            var destFile = Path.Combine(destBase, relativePath);
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(destFile)!);
            fileSystem.CopyFile(sourceFile, destFile);
        }
        log.Verbose($"Copied staged files to repository at {destBase}");
    }),
};
```

`SpecialVariables.GitRepositoryTarget.DestinationPath` is `"Octopus.Action.Git.DestinationPath"` and is defined in `Calamari.Deployment.SpecialVariables` (already imported).

### Step 5 — Expose repo path to the transform script

- [ ] **Update the `transformRepository` first delegate to set the repo path variable**

The transform script runs with CWD = `transformsDirectory`. Setting `Octopus.Calamari.Git.RepositoryPath` lets the script locate the cloned repo without hardcoding a path.

Change the first delegate inside `transformRepository` from:

```csharp
new DelegateInstallConvention(d =>
                              {
                                  d.StagingDirectory = transformsDirectory;
                                  d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                  WriteVariableScriptToFile(d);
                              }),
```

To:

```csharp
new DelegateInstallConvention(d =>
                              {
                                  d.StagingDirectory = transformsDirectory;
                                  d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                  d.Variables.Set("Octopus.Calamari.Git.RepositoryPath", clonedRepository!.WorkingDirectory);
                                  WriteVariableScriptToFile(d);
                              }),
```

### Step 6 — Fill `commitToRemote`

- [ ] **Replace the empty `commitToRemote` list with stage/commit/push delegate**

Replace:

```csharp
var commitToRemote = new List<IConvention>
{

};
```

With:

```csharp
var commitToRemote = new List<IConvention>
{
    new DelegateInstallConvention(d =>
    {
        var commitParams = repositoryConfig!.CommitParameters;

        // Stage all changes — handles files added, modified, or deleted by the transform script
        clonedRepository!.StageAllChanges();

        // Commit — returns false if the working tree is clean
        if (!clonedRepository.CommitChanges(commitParams.Summary, commitParams.Description))
        {
            log.Info("No changes to commit.");
            return;
        }

        clonedRepository.PushChanges(
            commitParams.RequiresPr,
            commitParams.Summary,
            commitParams.Description,
            repositoryConfig.gitConnection.GitReference,
            CancellationToken.None).GetAwaiter().GetResult();
    })
};
```

### Step 7 — Fix the duplicate convention list and wrap in `try/finally`

- [ ] **Fix line 149 and wrap `RunConventions` in `try/finally`**

The bottom of `Execute()` currently reads:

```csharp
conventions.AddRange(stageTransformScriptAndSubstitute);
conventions.AddRange(stagePackagesToIncludeInRepository);
conventions.AddRange(repositoryOperations);
conventions.AddRange(transformRepository);
conventions.AddRange(stagePackagesToIncludeInRepository);   // BUG: duplicate
conventions.AddRange(commitToRemote);

var conventionRunner = new ConventionProcessor(deployment, conventions, log);
conventionRunner.RunConventions();
var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, pathToPackage);
return exitCode.Value;
```

Replace it with:

```csharp
conventions.AddRange(stageTransformScriptAndSubstitute);
conventions.AddRange(stagePackagesToIncludeInRepository);
conventions.AddRange(repositoryOperations);
conventions.AddRange(transformRepository);
conventions.AddRange(commitToRemote);

try
{
    var conventionRunner = new ConventionProcessor(deployment, conventions, log);
    conventionRunner.RunConventions();
}
finally
{
    clonedRepository?.Dispose();
}

var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, pathToPackage);
return exitCode.Value;
```

### Step 8 — Build and commit

- [ ] **Build the full solution**

```bash
dotnet build source/Calamari/Calamari.csproj
```

Expected: build succeeds with no errors.

- [ ] **Run platform-agnostic tests to confirm no regressions**

```bash
dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "Category=PlatformAgnostic"
```

Expected: all tests pass.

- [ ] **Commit**

```bash
git add source/Calamari/Commands/CommitToGitCommand.cs
git commit -m "feat: implement clone, copy, and commit conventions in CommitToGitCommand"
```
