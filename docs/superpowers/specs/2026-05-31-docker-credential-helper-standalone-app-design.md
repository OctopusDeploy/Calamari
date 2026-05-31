# Docker Credential Helper as a Standalone App — Design

## Background

PR [#1542](https://github.com/OctopusDeploy/Calamari/pull/1542) adds a Docker credential
helper so that pulling container images no longer emits the warning:

```
WARNING! Your credentials are stored unencrypted in 'octo-docker-configs/config.json'.
Configure a credential helper to remove this warning.
```

The original implementation makes the credential helper a **Calamari subcommand**
(`calamari docker-credential`). Because Docker's credential-helper protocol communicates
over stdin/stdout, and a normal Calamari boot writes startup logs to stdout (which would
corrupt that protocol), the PR had to alter Calamari's **core startup/logging plumbing**:
`DeferredLogger`, `IWantCustomHandlingOfDeferredLogs`, and changes to `Program.cs` /
`CalamariFlavourProgram.cs`. Those touch paths used by every command, and reviewers
rejected the approach as too invasive.

## Goal

Re-implement the credential helper as a **separate standalone executable** that Docker
invokes directly, so none of Calamari's shared startup/logging plumbing needs to change.
The credential protocol runs on the helper's own clean stdin/stdout.

## Non-goals

- Changing the encryption scheme or credential file format.
- Changing the feature-toggle gating (`UseDockerCredentialHelperFeatureToggle` stays).
- Adding `list`/`version` credential operations (only `get`/`store`/`erase` are needed for
  the login + pull flow).

## Architecture & components

### New project: `source/Calamari.DockerCredentialHelper/`

- `net8.0`, `OutputType=Exe`, `AssemblyName=docker-credential-octopus`.
- Same `RuntimeIdentifiers` as Calamari: `win-x64;linux-x64;osx-x64;linux-arm;linux-arm64`.
- References `Calamari.Common` (for `AesEncryption`) and the shared credential store below.
- `Program.Main` is deliberately minimal — **no host, no DI, no logging framework**:
  1. Parse the operation from `argv[0]` (`get` / `store` / `erase`).
  2. Read `OCTOPUS_CREDENTIAL_PASSWORD` and `DOCKER_CONFIG` from the environment.
  3. Run the Docker credential protocol over stdin/stdout.
  4. Write only protocol output to stdout, errors to stderr, return the exit code.

  Because it never boots the Calamari host, there is nothing to corrupt the protocol and
  nothing to defer — which is precisely why no shared startup/logging code changes.

### Shared credential store (extracted)

Extract the crypto + file storage out of today's `DockerCredentialHelper` into a
`DockerCredentialStore` class in `Calamari.Common`, so the helper exe and the downloader
share one copy:

- `Store(serverUrl, username, secret, encryptionPassword, dockerConfigPath)`
- `Get(serverUrl, encryptionPassword, dockerConfigPath)` → credential or null
- `Erase(serverUrl, dockerConfigPath)`
- `GetCredentialFileName(serverUrl)` (base64url-of-server-URL + `.cred`)

Encryption uses `AesEncryption.ForScripts(encryptionPassword)`
(`Calamari.Common.Plumbing.Extensions`), unchanged from the PR.

### Downloader side (`Calamari.Shared`, `DockerImagePackageDownloader` + setup/cleanup)

Keep the orchestration (`SetupCredentialHelper`, `CreateDockerConfig`,
`CleanupCredentialHelper`, `GetServerUrlForCredentialHelper`), but:

- **Locate** `docker-credential-octopus(.exe)` next to the Calamari executable and add that
  directory to `PATH` (instead of extracting `.sh`/`.ps1` wrapper scripts).
- Set `DOCKER_CONFIG` and `OCTOPUS_CREDENTIAL_PASSWORD` for the duration of the pull.
  Docker passes the environment through to the credential helper process.
- Remove script extraction and the `OCTOPUS_CALAMARI_EXECUTABLE` indirection.

## Build & packaging

A purely framework-dependent helper cannot reliably bind to the runtime inside Calamari's
**self-contained** publish folder (no registered shared framework there, just loose runtime
DLLs). The mechanically sound way to get a small co-located footprint is:

**Build the helper self-contained per RID, then overlay it into Calamari's publish folder.**

- The helper project declares the same 5 RIDs and publishes self-contained, like Calamari.
- In the Nuke publish step (`build/Build.PackageCalamariProjects.cs`), after publishing the
  helper for a RID, copy its publish output into the matching Calamari RID publish directory
  **before** consolidation/compression.
- Both being self-contained for the same RID, the runtime DLLs (`coreclr`, `System.*`, …) and
  `Calamari.Common.dll` are byte-identical and simply coincide on overlay. The only net-new
  files are `docker-credential-octopus(.exe)`, `docker-credential-octopus.dll`, and its
  `.deps.json` / `.runtimeconfig.json`.
- A self-contained exe loads its runtime from its own directory, so the overlaid helper runs
  correctly sitting beside `Calamari(.exe)`. Net added size per RID is a few small files.

This reuses the existing per-RID publish loop and consolidation/dedupe machinery rather than
introducing a parallel packaging path. `docker-credential-octopus` ships inside the Calamari
package, next to `Calamari`, where the downloader expects it on `PATH`.

To verify during implementation:
- The overlay does not clobber Calamari's own DLLs with differing builds (same source/config/RID
  → should match).
- The helper apphost survives consolidation + compression into the final package.
- The copy from the helper published output to the calamari location should verify files are identical when copying

## Login-failure fallback

Keep a fallback so a misbehaving helper degrades to today's behavior rather than failing a
deployment:

- While the helper is enabled, a **non-zero `docker login` exit code** triggers fallback handling.
- On a non-zero exit, attempt to match the known credential-helper error in the captured login
  output (e.g. Docker's `"Error saving credentials"`). This match is **diagnostic only** — it
  lets us log *why* we fell back when the cause is recognizable.
- **Whether or not the string matches, the non-zero exit triggers the fallback**: clean up the
  helper config and retry `docker login` **once** without the helper. The string match never
  gates the fallback; it only distinguishes a known credential-helper failure from an unknown one.

Because we still inspect login output for the diagnostic match, the stdout-capture path is
**retained**: the contained, backwards-compatible `CommandLineRunner` optional-sink change and
`InMemoryCommandOutputSink` stay. (The invasive startup/logging changes are still reverted —
they were never part of this fallback.)

## Reverts from the current PR

These are removed entirely:

- `source/Calamari/Commands/DockerCredentialCommand.cs`
- `source/Calamari.Common/Plumbing/Logging/DeferredLogger.cs`
- `source/Calamari.Common/Commands/IWantCustomHandlingOfDeferredLogs.cs`
- `Program.cs` / `CalamariFlavourProgram.cs` changes (DeferredLogger wiring)
- `docker-credential-octopus.ps1` / `docker-credential-octopus.sh` wrapper scripts
- The `OCTOPUS_CALAMARI_EXECUTABLE` env var and `GetCalamariExecutablePath()` logic

`InMemoryCommandOutputSink` and the `CommandLineRunner` optional-sink change are **retained** —
the fallback's diagnostic string match still needs to inspect login output (see Login-failure
fallback above).

## Testing

- **Unit tests** for `DockerCredentialStore`: encrypt→decrypt round-trip; `GetCredentialFileName`
  encoding; get-on-missing-file returns null; erase.
- **Integration test** invoking the *built* `docker-credential-octopus` binary as a subprocess:
  pipe a `store` payload in, then `get` the same server URL back, asserting protocol JSON on
  stdout and exit codes — exercising the real executable end-to-end.
- **Keep** the `DockerImagePackageDownloader` integration test (behind the feature toggle),
  updated to assert the helper is located/added to `PATH` rather than scripts being extracted.
- **Drop** `DockerCredentialScriptsFixture` (the `.sh`/`.ps1` scripts no longer exist).

## Open implementation notes

- The encryption-password sourcing in the PR
  (`variables.Get("Octopus.Action.Package.DownloadOnTentacle") ?? variables.Get("SensitiveVariablesPassword") ?? "DefaultFallbackPassword"`)
  looks suspect (`DownloadOnTentacle` is a flag, not a password). Confirm the correct sensitive
  variable password source during implementation; out of scope to redesign here but should not
  be carried over blindly.
