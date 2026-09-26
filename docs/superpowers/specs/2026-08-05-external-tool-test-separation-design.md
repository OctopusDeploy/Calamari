# External Tool Test Separation — Design (Infrastructure + Terraform)

## Problem

Calamari's test suite mixes tests that depend on external CLI tools (Terraform, Helm, kubectl, Azure CLI, etc.) into the main pipeline. This:

- Slows the main feedback loop and couples it to external tool/service availability
- Pins tool versions inline in fixtures with no central tracking or version lifecycle
- Has no systematic way to know what versions we support or test against

Two prior efforts explored fixes and left the direction unreconciled:

- `external-tool-test-categorisation` / `robe/terraform-external-tool-categorization` — in-place `[Category(ExternalToolIntegration)]` tagging, no new project.
- `feature/external-tool-test-separation` — a new `Calamari.ExternalTools.Tests` project with a tool-version manifest and download/resolution infrastructure ("custom tooling mechanism").

## Decision

The separate-project approach (`feature/external-tool-test-separation`) is canonical. It supersedes the in-place categorisation branches, which are no longer pursued. This branch (`robe/external-tool-test-separation`) reimplements that approach fresh against current `main` rather than cherry-picking, because:

- Main has drifted from where the feature branch forked (unrelated churn: NSubstitute/Shouldly swaps, AKS/EKS version bumps)
- Several feature-branch commits bundle multiple tools together (e.g. one commit adds download strategies for all 8 tools at once), so they don't cherry-pick cleanly for a Terraform-only slice

The feature branch's own implementation plan (`docs/superpowers/plans/2026-06-15-external-tool-test-separation.md`, Tasks 1-7) already scopes almost exactly to infrastructure + Terraform, and is used as the reference blueprint.

## Scope

**In scope for this branch:**
- `Calamari.ExternalTools.Tests` project scaffold + `tool-manifest.json`
- Shared infrastructure: `ToolManifest`, `ToolResolver`, `ToolDownloader`, `ExternalToolFixture`, `CalamariCommandHelper`
- Terraform: `TerraformStrategy`, Terraform integration tests migrated into the new project, Terraform unit tests added to `Calamari.Tests`, old `Calamari.Terraform.Tests` fixture trimmed/removed

**Explicitly out of scope for this branch** (left for follow-up branches/PRs):
- Helm, kubectl, Azure CLI, GCloud, AWS CLI, aws-iam-authenticator, kubelogin
- All `CloudIntegration/` (SDK-based, e.g. Azure App Service) tests
- Automated version-expansion scheduling, NUnit `[Category]` wiring into TeamCity/Nuke build targets (infrastructure for this can land, but pipeline wiring is future work)

## Architecture

A single new NUnit test project, isolated from the main pipeline:

```
Calamari.ExternalTools.Tests/
  tool-manifest.json
  Infrastructure/
    ToolManifest.cs             (manifest reader, version range support)
    ToolResolver.cs              (env var override -> PATH lookup -> download)
    ToolDownloader.cs            (download + cache, retry, platform/arch detection)
    ExternalToolFixture.cs       (base class for tool fixtures)
    CalamariCommandHelper.cs     (in-process Calamari command runner)
    ToolStrategies/
      TerraformStrategy.cs
  ExternalTools/
    Terraform/
      TerraformCommandsFixture.cs   (~5 integration tests, [Category("ExternalTool")])
```

`CloudIntegration/` is not created yet — nothing lands there this round — so the directory structure doesn't need to change shape when cloud tests are added later.

**Tool resolution order** (in `ToolResolver`):
1. `CALAMARI_TOOL_TERRAFORM_VERSION=X.Y.Z` — pins an exact version (for future automated version discovery)
2. `CALAMARI_TOOL_SKIP_DOWNLOAD=true` — PATH-only, fails loudly if not found (local dev)
3. Default — downloads the manifest's `highest` version (CI default, reproducible)

**Tool manifest** (`tool-manifest.json`) declares, per tool: `lowest` (contractual minimum), `highest` (latest verified), `source` (release endpoint for future automated discovery), `architectures` (`amd64`, `arm64`).

## Terraform Migration

- Add unit tests to `Calamari.Tests` (main pipeline) covering logic gaps identified in the original fixture: var-file argument construction, init command construction, version range checking.
- Delete `source/Calamari.Terraform.Tests/CommandsFixture.cs` and now-redundant resource fixtures/directories.
- Keep 2-3 "wiring" tests as integration tests in the new project — these validate things unit tests can't, like Octostache-substitution-before-Terraform ordering, sensitive-variable output parsing, and plan detailed exit code handling.
- Add ~5 integration tests in `ExternalTools/Terraform/`, tagged `[Category("ExternalTool")]`, run against the manifest's `highest` version by default.
- Remove `Calamari.Terraform.Tests` from `Calamari.sln`; add `Calamari.ExternalTools.Tests`.

## CI

`--filter "Category=ExternalTool"` isolates the new project's tests for a separate (nightly/on-demand) run, distinct from the default pipeline. Wiring this into TeamCity/Nuke build targets is follow-up work, not required for this branch to be mergeable — the category exists and is filterable even before a dedicated pipeline stage consumes it.

## Testing

- New unit tests run in the default `dotnet test` pipeline via `Calamari.Tests` — no infrastructure changes needed there.
- `Calamari.ExternalTools.Tests` requires either network access (to download Terraform) or a pre-installed Terraform on PATH with `CALAMARI_TOOL_SKIP_DOWNLOAD=true`; it is not expected to run in the default CI job yet.
- `ToolManifest` and `ToolResolver` get their own unit tests (manifest parsing, version range checks, resolution order) since they're new shared infrastructure other tools will depend on later.

## Out-of-scope follow-up (tracked, not blocking)

- Migrate Helm, kubectl, Azure CLI, GCloud, AWS CLI, aws-iam-authenticator, kubelogin tool tests
- Migrate Azure App Service, AzureResourceGroup, AzureWebApp, GoogleCloudScripting cloud tests into `CloudIntegration/`
- Automated version-expansion scheduled job (`[Explicit]` `ToolVersionExpansionFixture`, `LatestVersionFinder`)
- TeamCity pipeline stage + Nuke build target for the external-tool test run
