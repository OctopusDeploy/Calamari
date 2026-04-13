# Commit-to-Git Repository Conventions — Design Spec

**Date:** 2026-04-13
**Branch:** `tmm/new_commit_to_git`

---

## Overview

Implement the two empty convention groups in `CommitToGitCommand` (`repositoryOperations` and `commitToRemote`) by:

1. Creating the `RepositoryWrapper` at command level (as a captured local variable, matching the existing pattern for `baseWorkingDirectory`, `inputsDirectory`, etc.)
2. Using `DelegateInstallConvention` instances to clone, copy files, and commit/push — the delegates close over the `RepositoryWrapper`
3. Disposing the `RepositoryWrapper` after `RunConventions()` returns

No new convention interfaces are required. The repository is never exposed to external consumers.

---

## State at Command Level

Three new local variables are added to `CommitToGitCommand.Execute()`, following the existing pattern:

```csharp
RepositoryWrapper? clonedRepository = null;
// inputsDirectory already exists
// transformsDirectory already exists
```

`clonedRepository` is nullable and set by the first delegate in `repositoryOperations`. All subsequent delegates that use it can assume it is non-null (the convention processor runs them in order).

`clonedRepository` is disposed after `conventionRunner.RunConventions()` returns:

```csharp
var conventionRunner = new ConventionProcessor(deployment, conventions, log);
try
{
    conventionRunner.RunConventions();
}
finally
{
    clonedRepository?.Dispose();
}
```

---

## `repositoryOperations` Convention Group

Replaces the current empty list.

### Delegate 1 — Clone

```csharp
new DelegateInstallConvention(d =>
{
    var config = configFactory.CreateCommitToGitRepositoryConfig(d);
    clonedRepository = repositoryFactory.CloneRepository("git_repository", config.gitConnection);
})
```

Reads URL, credentials, and branch from `SpecialVariables.GitRepositoryTarget.*` via `DeploymentConfigFactory`.

### Delegate 2 — Copy files into repository

```csharp
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
})
```

Copies all staged input files from `inputsDirectory` into `repository.WorkingDirectory/{destinationPath}` preserving relative paths.

---

## `transformRepository` Convention Group

Unchanged from current implementation — the transform script runs with CWD = `transformsDirectory`. A single addition: expose the repo working directory as a deployment variable so the script can find the repo:

```csharp
new DelegateInstallConvention(d =>
{
    d.StagingDirectory = transformsDirectory;
    d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
    d.Variables.Set("Octopus.Calamari.Git.RepositoryPath", clonedRepository!.WorkingDirectory);
    WriteVariableScriptToFile(d);
}),
new ExecuteScriptConvention(scriptEngine, commandLineRunner, log)
```

---

## `commitToRemote` Convention Group

Replaces the current empty list.

The transform script may have added, modified, or deleted files within the cloned repository. To stage all of these correctly, `RepositoryWrapper` needs a new `StageAllChanges()` method (see below).

```csharp
new DelegateInstallConvention(d =>
{
    var commitParams = configFactory.CommitParameters(d);

    // Stage all changes — new, modified, and deleted
    clonedRepository!.StageAllChanges();

    // Commit (no-op if nothing changed)
    if (!clonedRepository.CommitChanges(commitParams.Summary, commitParams.Description))
    {
        log.Info("No changes to commit.");
        return;
    }

    // Push
    clonedRepository.PushChanges(
        commitParams.RequiresPr,
        commitParams.Summary,
        commitParams.Description,
        configFactory.CreateCommitToGitRepositoryConfig(d).gitConnection.GitReference,
        CancellationToken.None).GetAwaiter().GetResult();
})
```

---

## New Method: `RepositoryWrapper.StageAllChanges()`

**Location:** `source/Calamari/ArgoCD/Git/RepositoryWrapper.cs`

Uses `LibGit2Sharp.Commands.Stage` with `"*"` — equivalent to `git add -A`. Stages new, modified, and deleted files in a single call:

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
```

---

## Changes to `CommitToGitCommand`

### New constructor parameters

- `DeploymentConfigFactory configFactory`
- `IRepositoryFactory repositoryFactory`

Both are registered in Autofac and injected automatically.

### Bug fix

Line 149 duplicates `conventions.AddRange(stagePackagesToIncludeInRepository)`. It is removed along with the now-empty `transformRepository` and `commitToRemote` lists being replaced by the new delegates above.

### `DeploymentConfigFactory.CommitParameters` visibility

Must be changed from `private` → `public` (needed by `commitToRemote` delegate).

---

## Files

### Modify
| File | Change |
|---|---|
| `Commands/CommitToGitCommand.cs` | Add constructor params, wire all three delegate groups, dispose repository, fix duplicate line |
| `ArgoCD/Conventions/DeploymentConfigFactory.cs` | Make `CommitParameters` public |
| `ArgoCD/Git/RepositoryWrapper.cs` | Add `StageAllChanges()` method |

### Delete
| File | Reason |
|---|---|
| `Deployment/Conventions/GitRepositoryConvention.cs` | Replaced by inline delegates |

### No new files required

---

## Out of Scope

- Output variables (commit SHA as Octopus output variable)
- Purge-output-directory behaviour
- `CommitToGitConvention.cs` in `ArgoCD/Conventions/` — unrelated ArgoCD-specific skeleton; untouched
