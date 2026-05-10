# CommitToGit Custom Properties Loader — Design

**Date:** 2026-05-11
**Status:** Approved (pending implementation)

## Goal

Move git credentials for the `commit-to-git` command out of `IVariables` and into an
encrypted JSON properties file, using the same `CustomPropertiesLoader` pattern that
the Argo CD commands already use. The file is mandatory: if it cannot be loaded, the
command fails with a `CommandException` and does not fall back to variables.

## Context

`Calamari.Common.Plumbing.Variables.CustomPropertiesLoader` already exists and is
used by `UpdateArgoCDAppImagesCommand` and `UpdateArgoCDAppManifestsCommand`. Both
Argo commands:

1. Declare `--customPropertiesFile=` and `--customPropertiesPassword=` CLI options.
2. Construct `new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword)`.
3. Pass the loader into a convention, which calls `loader.Load<ArgoCDCustomPropertiesDto>()`.

The Argo DTO (in `Calamari.Contracts`, namespace `Octopus.Calamari.Contracts.ArgoCD`) is:

```csharp
public record ArgoCDCustomPropertiesDto(
    ArgoCDGatewayDto[] Gateways,
    ArgoCDApplicationDto[] Applications,
    GitCredentialDto[] Credentials);

public record GitCredentialDto(string Url, string Username, string Password);
```

`CommitToGitConfigFactory.CreateRepositoryConfig` currently reads
`SpecialVariables.Action.Git.Username` and `SpecialVariables.Action.Git.Password`
directly from `IVariables` to build a `GitConnection`. After this change those reads
go away; the URL, ref, commit message, destination-path and pull-request flag
continue to come from variables as today.

## New Types

Location: `source/Calamari.Contracts/CommitToGit/CommitToGitCustomPropertiesDto.cs`
Namespace: `Octopus.Calamari.Contracts.CommitToGit`

```csharp
public record CommitToGitCustomPropertiesDto(NamedGitCredentialDto Credential);

public record NamedGitCredentialDto(string Username, string Password, string Name);
```

`Name` is the friendly name of the git credential as known to Octopus Server. It
is loaded for completeness/passthrough; Calamari is not required to use it.

The wrapping record matches the Argo shape so the JSON contract is symmetric across
the two flows and so additional top-level fields can be added later without breaking
existing payloads.

## Command Changes — `CommitToGitCommand`

1. Add two CLI options, identical in surface to the Argo commands:
   - `--customPropertiesFile=` — path; stored as the full path via `Path.GetFullPath(v)`.
   - `--customPropertiesPassword=` — decryption password.
2. After `Options.Parse(commandLineArguments)`, validate both values are provided:
   - Missing file path → `throw new CommandException("Required option --customPropertiesFile was not provided.")`
   - Missing password → `throw new CommandException("Required option --customPropertiesPassword was not provided.")`
3. Construct `new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword)` and pass it to `configFactory.CreateRepositoryConfig(deployment, loader)`.

No other behaviour in `Execute` changes; the convention pipeline, journal entry, and
exit-code handling remain as-is.

## Factory Changes — `CommitToGitConfigFactory`

1. `CreateRepositoryConfig(RunningDeployment deployment)` becomes
   `CreateRepositoryConfig(RunningDeployment deployment, ICustomPropertiesLoader loader)`.
2. Inside, call `var props = loader.Load<CommitToGitCustomPropertiesDto>();` and use
   `props.Credential.Username` and `props.Credential.Password` to build the
   `GitConnection`.
3. Remove the two lines that previously read
   `variables.Get(SpecialVariables.Action.Git.Username)` and
   `variables.Get(SpecialVariables.Action.Git.Password)`.
4. Everything else — URL, reference, commit summary/description, pull-request flag,
   destination path — continues to come from `IVariables`.

## Data Flow

```
Octopus Server
  └─ writes encrypted JSON {Credential:{Username,Password,Name}}
       │
       ▼
  --customPropertiesFile=<path>  +  --customPropertiesPassword=<password>
       │
       ▼
  CustomPropertiesLoader.Load<CommitToGitCustomPropertiesDto>()
       │
       ▼
  CommitToGitConfigFactory.CreateRepositoryConfig(deployment, loader)
       │       ┌──────────────────────────────────┐
       │       │ Url, Reference, CommitMessage*,  │
       │       │ DestinationPath, PullRequest.*    │   ← still from IVariables
       │       └──────────────────────────────────┘
       ▼
  GitConnection(username, password, url, reference)  → CommitToGitRepositorySettings
```

## Error Handling

| Failure | Source | Result |
|---|---|---|
| `--customPropertiesFile` missing | New validation in `CommitToGitCommand.Execute` | `CommandException("Required option --customPropertiesFile was not provided.")` |
| `--customPropertiesPassword` missing | New validation in `CommitToGitCommand.Execute` | `CommandException("Required option --customPropertiesPassword was not provided.")` |
| File path does not exist / unreadable | `ICalamariFileSystem.ReadAllBytes` | Wrap in `CommandException` inside `CustomPropertiesLoader.Load` (small extension to existing loader, or handle in caller). Surface a clear message that the properties file could not be read. |
| Decrypt fails | `CustomPropertiesLoader.Decrypt` (existing) | Existing `CommandException("Cannot decrypt custom properties. Check your password is correct.")` |
| JSON parse fails | `CustomPropertiesLoader.Load` (existing) | Existing `CommandException("Unable to parse custom properties as valid JSON.")` |
| Loaded `Credential` is null | New: factory-level guard | `CommandException("Custom properties file did not contain a git credential.")` |

The existing `CustomPropertiesLoader` does not currently wrap `ReadAllBytes` failures
in a `CommandException`. The cleanest, least-disruptive change is to handle the
`FileNotFoundException` / `DirectoryNotFoundException` at the call-site (or as a
narrow try/catch around `Load<T>()`), rather than changing the shared loader's
contract for Argo's sake. The implementation plan should pick one of those two; the
preferred choice is to keep the shared loader untouched.

## Testing

**Factory unit tests** (`CommitToGitConfigFactoryTests` — new test file, or extend
existing if one exists):

- Happy path: given a stub `ICustomPropertiesLoader` returning a known
  `CommitToGitCustomPropertiesDto`, assert the resulting `CommitToGitRepositorySettings`
  carries the credential's username/password and the URL/reference from variables.
- Null `Credential` in the loaded DTO → `CommandException` with the message above.
- Missing `SpecialVariables.Action.Git.Url` → existing `CommandException` still
  thrown (unchanged behaviour, regression guard).

**Command-level tests** (`CommitToGitCommandTest`):

- Update existing scaffolding to produce an encrypted properties file using
  `AesEncryption.ForServerVariables(password)` and pass
  `--customPropertiesFile=<temp path>` and `--customPropertiesPassword=<password>`.
- New case: omit `--customPropertiesFile` → expect `CommandException` mentioning the
  missing option.
- New case: omit `--customPropertiesPassword` → expect `CommandException` mentioning
  the missing option.
- New case: file path points at a non-existent file → expect a `CommandException`.

## Files Touched

| File | Change |
|---|---|
| `source/Calamari.Contracts/CommitToGit/CommitToGitCustomPropertiesDto.cs` | New |
| `source/Calamari/Commands/CommitToGitCommand.cs` | Add options, validate, construct loader, pass to factory |
| `source/Calamari/CommitToGit/CommitToGitConfigFactory.cs` | New signature, load DTO, drop username/password variable reads |
| `source/Calamari.Tests/CommitToGitCommandTest.cs` | Update to provide encrypted file; add missing-option cases |
| `source/Calamari.Tests/CommitToGit/CommitToGitConfigFactoryTests.cs` (or similar) | New unit tests, if a factory-level test file does not already exist |

## Non-Goals / Out of Scope

- Migrating Argo's `GitCredentialDto` to include a `Name` field. The two flows keep
  their own DTOs so neither contract is destabilised by the other.
- Supporting multiple credentials in CommitToGit. The DTO holds one `Credential`.
- Changing where URL, reference, commit message, destination path or pull-request
  flag come from.
- Calamari making any actual use of `Name` — it is loaded but not consumed.
