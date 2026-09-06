# External Tool Test Separation (Infrastructure + Terraform) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a new `Calamari.ExternalTools.Tests` project with a Terraform-only tool-version manifest and shared download/resolution infrastructure, migrate Terraform's tool-dependent integration tests into it, and add real unit-test coverage for the Terraform CLI argument-construction logic that currently has no coverage — all without touching the in-place categorisation branches.

**Architecture:** A single new NUnit test project reads the Terraform entry from a co-located `tool-manifest.json`. Shared infrastructure resolves the tool via env var override → PATH lookup → download-and-cache. The existing `Calamari.Terraform.Tests` project is trimmed (its large cloud/tool integration fixture moves out) but not deleted — three already-mocked unit fixtures stay there and gain new coverage.

**Tech Stack:** C# / .NET 8.0, NUnit 3.14.0, FluentAssertions 7.2.0, NSubstitute 6.0.0, System.Text.Json 9.0.16

## Global Constraints

- Package versions must match what's already pinned elsewhere in the repo: `FluentAssertions 7.2.0`, `NUnit 3.14.0`, `NUnit3TestAdapter 5.2.0`, `Microsoft.NET.Test.Sdk 18.0.0`, `TeamCity.VSTest.TestAdapter 1.0.41`, `System.Text.Json 9.0.16`.
- No `SharpCompress` dependency is added in this plan — Terraform only needs zip extraction (`System.IO.Compression.ZipFile`, already in the BCL). Tar.gz support is added when a tool that needs it (Helm, GCloud, ...) lands in a later branch.
- `tool-manifest.json` contains only a `terraform` entry. Other tools are added when their migration branch lands.
- **Deviation from the approved design spec:** the spec said new Terraform unit tests would live in `Calamari.Tests`. Investigation during planning found `Calamari.Terraform.Tests` already has real (not reimplemented) access to `Calamari.Terraform`'s internals via `InternalsVisibleTo`, and an existing mocked fixture (`TerraformCliExecutorFixture`) already exercises the executor via NSubstitute. Adding new tests there — against the real production methods — is stronger coverage than duplicating the logic in a separate project, and `Calamari.Terraform.Tests` already runs in the default (main) pipeline, so the "unit tests land in the main pipeline before integration tests are trimmed" requirement is still met. Task 7 below reflects this.

---

### Task 1: Scaffold the `Calamari.ExternalTools.Tests` project

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj`
- Create: `source/Calamari.ExternalTools.Tests/tool-manifest.json`
- Modify: `source/Calamari.sln`
- Modify: `source/Calamari.Terraform/Properties/InternalsVisibleTo.cs`

- [ ] **Step 1: Create the project directory structure**

```bash
mkdir -p source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies
mkdir -p source/Calamari.ExternalTools.Tests/ExternalTools/Terraform
```

- [ ] **Step 2: Create the .csproj file**

Create `source/Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <RootNamespace>Calamari.ExternalTools.Tests</RootNamespace>
        <AssemblyName>Calamari.ExternalTools.Tests</AssemblyName>
        <RuntimeIdentifiers>win-x64;linux-x64;osx-x64;linux-arm;linux-arm64</RuntimeIdentifiers>
        <IsPackable>false</IsPackable>
        <TargetFramework>net8.0</TargetFramework>
        <!-- CS8632: The annotation for nullable reference types should only be used in code within a '#nullable' annotations context. -->
        <NoWarn>CS8632</NoWarn>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="FluentAssertions" Version="7.2.0" />
        <PackageReference Include="NUnit" Version="3.14.0" />
        <PackageReference Include="NUnit3TestAdapter" Version="5.2.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
        <PackageReference Include="TeamCity.VSTest.TestAdapter" Version="1.0.41" />
        <PackageReference Include="System.Text.Json" Version="9.0.16" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Calamari.Testing\Calamari.Testing.csproj" />
        <ProjectReference Include="..\Calamari.Common\Calamari.Common.csproj" />
        <ProjectReference Include="..\Calamari.Terraform\Calamari.Terraform.csproj" />
    </ItemGroup>

    <ItemGroup>
        <None Update="tool-manifest.json">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
        <None Update="**/*.tf*">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
        <None Update="**/*.json">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
        <None Update="**/*.txt">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the tool manifest**

Create `source/Calamari.ExternalTools.Tests/tool-manifest.json`. The range matches `TerraformCliExecutor`'s own `supportedVersionRange` (`source/Calamari.Terraform/TerraformCliExecutor.cs:37`) — `0.13.7` inclusive to `1.9` exclusive:

```json
{
  "tools": {
    "terraform": {
      "lowest": "0.13.7",
      "highest": "1.8.5",
      "source": "https://releases.hashicorp.com/terraform/",
      "architectures": ["amd64", "arm64"]
    }
  }
}
```

- [ ] **Step 4: Add the project to the solution**

```bash
cd source && dotnet sln Calamari.sln add Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 5: Grant the new project visibility into Calamari.Terraform's internals**

`TerraformSpecialVariables` (`source/Calamari.Terraform/TerraformSpecialVariables.cs:6`) is an internal static class used throughout the Terraform test fixtures. Modify `source/Calamari.Terraform/Properties/InternalsVisibleTo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Calamari.Terraform.Tests")]
[assembly: InternalsVisibleTo("Calamari.ExternalTools.Tests")]
```

- [ ] **Step 6: Verify the solution builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

Expected: Build succeeds with no errors (no source files yet, just the empty scaffold).

- [ ] **Step 7: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/ source/Calamari.sln source/Calamari.Terraform/Properties/InternalsVisibleTo.cs
git commit -m "feat: scaffold Calamari.ExternalTools.Tests project with Terraform tool manifest"
```

---

### Task 2: Implement the manifest reader

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifest.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifestTests.cs`

**Interfaces:**
- Produces: `ToolManifest.Load()` → `ToolManifest`; `ToolManifest.GetTool(string name)` → `ToolDefinition?`; `ToolManifest.ToolNames` → `IReadOnlyCollection<string>`; `ToolDefinition.Lowest`/`.Highest` → `Version`; `ToolDefinition.IsInRange(Version)` → `bool`. Task 3 (`ToolResolver`) and Task 6 (`ExternalToolFixture`) consume these.

- [ ] **Step 1: Write the failing tests**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifestTests.cs`:

```csharp
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    [TestFixture]
    public class ToolManifestTests
    {
        [Test]
        public void ShouldLoadManifestFromEmbeddedFile()
        {
            var manifest = ToolManifest.Load();

            manifest.Should().NotBeNull();
            manifest.GetTool("terraform").Should().NotBeNull();
            manifest.GetTool("terraform")!.Lowest.ToString().Should().Be("0.13.7");
            manifest.GetTool("terraform")!.Highest.ToString().Should().Be("1.8.5");
        }

        [Test]
        public void ShouldReturnNullForUnknownTool()
        {
            var manifest = ToolManifest.Load();

            manifest.GetTool("nonexistent-tool").Should().BeNull();
        }

        [Test]
        public void ShouldCheckVersionIsInRange()
        {
            var manifest = ToolManifest.Load();
            var terraform = manifest.GetTool("terraform")!;

            terraform.IsInRange(new System.Version(1, 0, 0)).Should().BeTrue();
            terraform.IsInRange(new System.Version(0, 12, 0)).Should().BeFalse();
            terraform.IsInRange(new System.Version(2, 0, 0)).Should().BeFalse();
        }

        [Test]
        public void ShouldListAllTools()
        {
            var manifest = ToolManifest.Load();

            manifest.ToolNames.Should().Contain("terraform");
            manifest.ToolNames.Should().HaveCount(1);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~ToolManifestTests" -v minimal
```

Expected: Compilation error — `ToolManifest` does not exist.

- [ ] **Step 3: Implement the manifest reader**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifest.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calamari.Testing.Helpers;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    public class ToolManifest
    {
        readonly Dictionary<string, ToolDefinition> tools;

        ToolManifest(Dictionary<string, ToolDefinition> tools)
        {
            this.tools = tools;
        }

        public IReadOnlyCollection<string> ToolNames => tools.Keys.ToList();

        public ToolDefinition? GetTool(string name)
        {
            return tools.TryGetValue(name, out var tool) ? tool : null;
        }

        public static ToolManifest Load()
        {
            var manifestPath = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "tool-manifest.json");
            var json = File.ReadAllText(manifestPath);
            var doc = JsonSerializer.Deserialize<ManifestDocument>(json)
                      ?? throw new InvalidOperationException("Failed to deserialize tool-manifest.json");

            var tools = new Dictionary<string, ToolDefinition>();
            foreach (var (name, entry) in doc.Tools)
            {
                tools[name] = new ToolDefinition(
                    name,
                    ParseVersion(entry.Lowest),
                    ParseVersion(entry.Highest),
                    entry.Source,
                    entry.Architectures);
            }

            return new ToolManifest(tools);
        }

        static Version ParseVersion(string version)
        {
            var clean = version.TrimStart('v');
            return Version.Parse(clean);
        }

        class ManifestDocument
        {
            [JsonPropertyName("tools")]
            public Dictionary<string, ManifestEntry> Tools { get; set; } = new();
        }

        class ManifestEntry
        {
            [JsonPropertyName("lowest")]
            public string Lowest { get; set; } = "";

            [JsonPropertyName("highest")]
            public string Highest { get; set; } = "";

            [JsonPropertyName("source")]
            public string Source { get; set; } = "";

            [JsonPropertyName("architectures")]
            public string[] Architectures { get; set; } = Array.Empty<string>();
        }
    }

    public class ToolDefinition
    {
        public ToolDefinition(string name, Version lowest, Version highest, string source, string[] architectures)
        {
            Name = name;
            Lowest = lowest;
            Highest = highest;
            Source = source;
            Architectures = architectures;
        }

        public string Name { get; }
        public Version Lowest { get; }
        public Version Highest { get; }
        public string Source { get; }
        public string[] Architectures { get; }

        public bool IsInRange(Version version)
        {
            return version >= Lowest && version <= Highest;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~ToolManifestTests" -v minimal
```

Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifest*
git commit -m "feat: implement ToolManifest reader with version range support"
```

---

### Task 3: Implement the tool resolver

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolver.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolverTests.cs`

**Interfaces:**
- Consumes: `ToolManifest.GetTool(string)` (Task 2).
- Produces: `new ToolResolver(ToolManifest, Action<string> log)`; `.ResolveVersion(string toolName)` → `string`; `static ToolResolver.GetOverrideEnvVar(string toolName)` → `string`; `static ToolResolver.FindOnPath(string toolName)` → `string?`; `static ToolResolver.GetInstalledVersion(string executablePath, string versionArg = "--version")` → `string?`. Task 6 (`ExternalToolFixture`) consumes `ResolveVersion` and `FindOnPath`.

- [ ] **Step 1: Write the failing tests**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolverTests.cs`:

```csharp
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    [TestFixture]
    public class ToolResolverTests
    {
        [Test]
        public void ShouldBuildEnvironmentVariableOverrideName()
        {
            var envVarName = ToolResolver.GetOverrideEnvVar("terraform");
            envVarName.Should().Be("CALAMARI_TOOL_TERRAFORM_VERSION");
        }

        [Test]
        public void ShouldResolveToManifestHighestWhenNoOverrideSet()
        {
            var manifest = ToolManifest.Load();
            var resolver = new ToolResolver(manifest, _ => { });

            var version = resolver.ResolveVersion("terraform");

            version.Should().Be("1.8.5");
        }

        [Test]
        public void ShouldDetectToolOnPath()
        {
            // 'dotnet' is always on PATH in a .NET test run
            var result = ToolResolver.FindOnPath("dotnet");
            result.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void ShouldReturnNullForToolNotOnPath()
        {
            var result = ToolResolver.FindOnPath("definitely-not-a-real-tool-abc123");
            result.Should().BeNull();
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~ToolResolverTests" -v minimal
```

Expected: Compilation error — `ToolResolver` does not exist.

- [ ] **Step 3: Implement the tool resolver**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolver.cs`:

```csharp
using System;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Resolves the version to use for a tool: env var override, else the manifest's highest.
    /// Resolution of the actual executable (PATH vs download) is done by ExternalToolFixture.
    /// </summary>
    public class ToolResolver
    {
        readonly ToolManifest manifest;
        readonly Action<string> log;

        public ToolResolver(ToolManifest manifest, Action<string> log)
        {
            this.manifest = manifest;
            this.log = log;
        }

        public static string GetOverrideEnvVar(string toolName)
        {
            return $"CALAMARI_TOOL_{toolName.Replace("-", "_").ToUpperInvariant()}_VERSION";
        }

        public string ResolveVersion(string toolName)
        {
            var envVar = GetOverrideEnvVar(toolName);
            var overrideVersion = Environment.GetEnvironmentVariable(envVar);

            if (!string.IsNullOrEmpty(overrideVersion))
            {
                log($"Using override version {overrideVersion} for {toolName} (from {envVar})");
                return overrideVersion;
            }

            var tool = manifest.GetTool(toolName);
            if (tool == null)
                throw new InvalidOperationException($"Tool '{toolName}' not found in manifest");

            return tool.Highest.ToString();
        }

        public static string? FindOnPath(string toolName)
        {
            try
            {
                var command = CalamariEnvironment.IsRunningOnWindows ? "where" : "which";
                var executableName = CalamariEnvironment.IsRunningOnWindows
                    ? $"{toolName}.exe"
                    : toolName;

                var stdOut = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand(
                    command,
                    executableName,
                    ".",
                    s => stdOut.AppendLine(s),
                    _ => { });

                if (result.ExitCode == 0)
                {
                    var path = stdOut.ToString().Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return path.Length > 0 ? path[0] : null;
                }
            }
            catch
            {
                // Tool not found
            }

            return null;
        }

        public static string? GetInstalledVersion(string executablePath, string versionArg = "--version")
        {
            try
            {
                var stdOut = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand(
                    executablePath,
                    versionArg,
                    ".",
                    s => stdOut.AppendLine(s),
                    _ => { });

                return result.ExitCode == 0 ? stdOut.ToString().Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~ToolResolverTests" -v minimal
```

Expected: All 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolver*
git commit -m "feat: implement ToolResolver with env var override and manifest fallback"
```

---

### Task 4: Implement the tool downloader

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolDownloader.cs`

**Interfaces:**
- Produces: `new ToolDownloader(Action<string> log)`; `.Download(string toolName, string version, Func<string,string,HttpClient,Task<string>> downloadAction)` → `Task<string>` (the resolved executable path); `static ToolDownloader.DownloadFile(url, destinationPath, client)`; `static ToolDownloader.DownloadAndExtractZip(url, destinationDir, client)`; `static ToolDownloader.GetPlatform()` → `"windows"|"darwin"|"linux"`; `static ToolDownloader.GetArchitecture()` → `"amd64"|"arm64"`. Task 5 (`TerraformStrategy`) consumes `DownloadAndExtractZip`, `GetPlatform`, `GetArchitecture`. Task 6 (`ExternalToolFixture`) consumes `Download`.

No SharpCompress/tar.gz support here — Terraform only needs zip. Add tar.gz extraction when a tool that needs it lands.

- [ ] **Step 1: Implement the downloader**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolDownloader.cs`:

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Retry;
using Calamari.Testing.Helpers;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Downloads and caches external tool binaries.
    /// Cache location: {TestOutputDir}/Tools/{toolName}/{version}/
    /// </summary>
    public class ToolDownloader
    {
        readonly Action<string> log;

        public ToolDownloader(Action<string> log)
        {
            this.log = log;
        }

        public async Task<string> Download(string toolName, string version, Func<string, string, HttpClient, Task<string>> downloadAction)
        {
            var destinationDir = TestEnvironment.GetTestPath("Tools", toolName, version);

            var existing = FindExistingExecutable(toolName, destinationDir);
            if (existing != null)
            {
                log($"Using cached {toolName} {version} at {existing}");
                return existing;
            }

            log($"Downloading {toolName} {version}...");
            Directory.CreateDirectory(destinationDir);

            var retry = new RetryTracker(4, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(3000, 30000, 2));
            string? executablePath = null;

            while (retry.Try())
            {
                try
                {
                    using var client = CreateHttpClient();
                    executablePath = await downloadAction(destinationDir, version, client);
                    AddExecutePermission(executablePath);
                    break;
                }
                catch
                {
                    if (!retry.CanRetry())
                        throw;

                    await Task.Delay(retry.Sleep());
                }
            }

            log($"Downloaded {toolName} {version} to {executablePath}");
            return executablePath!;
        }

        string? FindExistingExecutable(string toolName, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
                return null;

            var path = Directory.EnumerateFiles(destinationDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return name.Contains(toolName.ToLowerInvariant().Replace("-", ""));
                });

            return path != null && File.Exists(path) ? path : null;
        }

        public static async Task DownloadFile(string url, string destinationPath, HttpClient client)
        {
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var stream = await client.GetStreamAsync(url);
            await stream.CopyToAsync(fileStream);
        }

        public static async Task DownloadAndExtractZip(string url, string destinationDir, HttpClient client)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
            try
            {
                await DownloadFile(url, tempPath, client);
                ZipFile.ExtractToDirectory(tempPath, destinationDir);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows || string.IsNullOrEmpty(exePath))
                return;

            SilentProcessRunner.ExecuteCommand(
                "chmod", $"+x {exePath}",
                Path.GetDirectoryName(exePath) ?? ".",
                _ => { }, _ => { });
        }

        static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36");
            return client;
        }

        public static string GetPlatform()
        {
            if (CalamariEnvironment.IsRunningOnWindows) return "windows";
            if (CalamariEnvironment.IsRunningOnMac) return "darwin";
            return "linux";
        }

        public static string GetArchitecture()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => "amd64"
            };
        }
    }
}
```

- [ ] **Step 2: Verify the project builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolDownloader.cs
git commit -m "feat: implement ToolDownloader with retry, caching, and platform detection"
```

---

### Task 5: Create the Terraform download strategy

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/TerraformStrategy.cs`

**Interfaces:**
- Consumes: `ToolDownloader.GetPlatform()`, `ToolDownloader.GetArchitecture()`, `ToolDownloader.DownloadAndExtractZip` (Task 4).
- Produces: `static TerraformStrategy.Download(string destinationDir, string version, HttpClient client)` → `Task<string>`. Task 6/8 consume this as the fixture's download delegate.

- [ ] **Step 1: Create the Terraform download strategy**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/TerraformStrategy.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class TerraformStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var platform = ToolDownloader.GetPlatform();
            var arch = ToolDownloader.GetArchitecture();
            var fileName = $"terraform_{version}_{platform}_{arch}.zip";
            var url = $"https://releases.hashicorp.com/terraform/{version}/{fileName}";

            await ToolDownloader.DownloadAndExtractZip(url, destinationDir, client);

            return Directory.EnumerateFiles(destinationDir)
                .First(f => Path.GetFileName(f).Contains("terraform"));
        }
    }
}
```

- [ ] **Step 2: Verify the project builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/
git commit -m "feat: add Terraform download strategy"
```

---

### Task 6: Create the ExternalToolFixture base class

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ExternalToolFixture.cs`

**Interfaces:**
- Consumes: `ToolManifest.Load()` (Task 2), `ToolResolver` (Task 3), `ToolDownloader` (Task 4).
- Produces: abstract base with `protected string ToolExecutablePath { get; }`, `protected string ToolVersion { get; }`, `protected abstract string PrimaryToolName { get; }`, `protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client)`. Task 8's `TerraformCommandsFixture` derives from this.

- [ ] **Step 1: Create the base fixture**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ExternalToolFixture.cs`:

```csharp
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    /// Resolves the tool via: env var override -> PATH -> download.
    /// Subclasses set PrimaryToolName and provide a download strategy.
    /// </summary>
    public abstract class ExternalToolFixture
    {
        static readonly ToolManifest Manifest = ToolManifest.Load();

        protected string ToolExecutablePath { get; private set; } = "";
        protected string ToolVersion { get; private set; } = "";

        protected abstract string PrimaryToolName { get; }

        protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client);

        [OneTimeSetUp]
        public async Task ResolveTool()
        {
            var resolver = new ToolResolver(Manifest, Log);
            var downloader = new ToolDownloader(Log);

            ToolVersion = resolver.ResolveVersion(PrimaryToolName);

            var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
            if (pathResult != null)
            {
                Log($"Found {PrimaryToolName} on PATH at {pathResult}");
                ToolExecutablePath = pathResult;
                return;
            }

            ToolExecutablePath = await downloader.Download(PrimaryToolName, ToolVersion, DownloadTool);
        }

        protected void Log(string message)
        {
            TestContext.Progress.WriteLine($"[{PrimaryToolName}] {message}");
        }
    }
}
```

- [ ] **Step 2: Verify the project builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ExternalToolFixture.cs
git commit -m "feat: add ExternalToolFixture base class for tool-dependent tests"
```

---

### Task 7: Add real unit-test coverage for Terraform CLI argument construction

**Files:**
- Modify: `source/Calamari.Terraform/Behaviours/TerraformDeployBehaviour.cs:62` (visibility bump only)
- Modify: `source/Calamari.Terraform.Tests/TerraformCliExecutorFixture.cs`

This closes the "logic gaps" the previous effort identified before removing the Terraform integration fixture: init command construction (the `-get-plugins` flag, version-gated at 0.15.0) and version-range checking (the "untested version" warning). `TerraformCliExecutorFixture` already tests `TerraformVariableFiles` (var-file args) directly against the real `TerraformCliExecutor` via NSubstitute — these new tests follow the exact same pattern, no test doubles for the logic itself.

**Interfaces:**
- Consumes: `TerraformCliExecutor` (existing, `source/Calamari.Terraform/TerraformCliExecutor.cs`), `TerraformDeployBehaviour.GetEnvironmentVariableArgs` (visibility bumped in Step 1).

- [ ] **Step 1: Bump `GetEnvironmentVariableArgs` from private to internal**

In `source/Calamari.Terraform/Behaviours/TerraformDeployBehaviour.cs:62`, change:

```csharp
        static Dictionary<string, string> GetEnvironmentVariableArgs(IVariables variables)
```

to:

```csharp
        internal static Dictionary<string, string> GetEnvironmentVariableArgs(IVariables variables)
```

This is a visibility-only change — no logic changes, no other call sites are affected. `Calamari.Terraform.Tests` already has `InternalsVisibleTo` access to `Calamari.Terraform`.

- [ ] **Step 2: Verify the project still builds**

```bash
cd source && dotnet build Calamari.Terraform/Calamari.Terraform.csproj
```

- [ ] **Step 3: Write the failing tests**

Add to `source/Calamari.Terraform.Tests/TerraformCliExecutorFixture.cs`, inside the existing `TerraformCliExecutorFixture` class (after `InitializePlugins_ThrowsAfterRetriesExhausted`):

```csharp
        [Test]
        public void InitCommand_PreV015_IncludesGetPluginsFlagTrue()
        {
            var capturedArguments = new List<string>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            testVariables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true).Returns(true);

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                capturedArguments.Add(invocation.Arguments);
                if (capturedArguments.Count == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.14.0");
            })).Returns(new CommandResult("terraform", 0));

            new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            capturedArguments[1].Should().Contain("-get-plugins=true");
        }

        [Test]
        public void InitCommand_PreV015_PluginDownloadsDisabled_IncludesGetPluginsFlagFalse()
        {
            var capturedArguments = new List<string>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            testVariables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true).Returns(false);

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                capturedArguments.Add(invocation.Arguments);
                if (capturedArguments.Count == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.14.0");
            })).Returns(new CommandResult("terraform", 0));

            new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            capturedArguments[1].Should().Contain("-get-plugins=false");
        }

        [Test]
        public void InitCommand_V015AndAbove_OmitsGetPluginsFlag()
        {
            var capturedArguments = new List<string>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                capturedArguments.Add(invocation.Arguments);
                if (capturedArguments.Count == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0");
            })).Returns(new CommandResult("terraform", 0));

            new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            capturedArguments[1].Should().NotContain("-get-plugins");
        }

        [Test]
        public void InitCommand_IncludesAdditionalInitParams()
        {
            var capturedArguments = new List<string>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            testVariables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams).Returns("-backend-config=\"key=value\"");

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                capturedArguments.Add(invocation.Arguments);
                if (capturedArguments.Count == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v1.0.0");
            })).Returns(new CommandResult("terraform", 0));

            new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            capturedArguments[1].Should().Contain("-backend-config=\"key=value\"");
            capturedArguments[1].Should().NotContain("-get-plugins");
        }

        [Test]
        public void UntestedVersion_AboveSupportedRange_LogsInfoOnSuccessfulCommand()
        {
            var log = Substitute.For<ILog>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v2.0.0");
            })).Returns(new CommandResult("terraform", 0));

            var executor = new TerraformCliExecutor(log, Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());
            executor.ExecuteCommand("plan");

            log.Received(1).Info(Arg.Is<string>(s => s.Contains("has not been tested")));
        }

        [Test]
        public void SupportedVersion_WithinRange_DoesNotLogUntestedMessage()
        {
            var log = Substitute.For<ILog>();
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());

            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v1.0.0");
            })).Returns(new CommandResult("terraform", 0));

            var executor = new TerraformCliExecutor(log, Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());
            executor.ExecuteCommand("plan");

            log.DidNotReceive().Info(Arg.Is<string>(s => s.Contains("has not been tested")));
            log.DidNotReceive().Warn(Arg.Is<string>(s => s.Contains("has not been tested")));
        }

        [Test]
        public void EnvironmentVariables_ParsedFromJson()
        {
            var variables = new CalamariVariables();
            variables.Set(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_VAR_ami", "test-value" }, { "TF_LOG", "DEBUG" } }));

            var result = TerraformDeployBehaviour.GetEnvironmentVariableArgs(variables);

            result.Should().ContainKey("TF_VAR_ami").WhoseValue.Should().Be("test-value");
            result.Should().ContainKey("TF_LOG").WhoseValue.Should().Be("DEBUG");
        }

        [Test]
        public void EnvironmentVariables_NotSet_ReturnsEmptyDictionary()
        {
            var variables = new CalamariVariables();

            var result = TerraformDeployBehaviour.GetEnvironmentVariableArgs(variables);

            result.Should().BeEmpty();
        }
```

Add these `using` statements to the top of `source/Calamari.Terraform.Tests/TerraformCliExecutorFixture.cs` (alongside the existing ones):

```csharp
using Calamari.Terraform.Behaviours;
using Newtonsoft.Json;
```

- [ ] **Step 4: Run the tests to verify they fail**

```bash
cd source && dotnet test Calamari.Terraform.Tests/ --filter "FullyQualifiedName~TerraformCliExecutorFixture" -v minimal
```

Expected: Compilation error — `TerraformDeployBehaviour.GetEnvironmentVariableArgs` inaccessible until Step 1 is applied; new test methods fail to compile/run until present.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd source && dotnet test Calamari.Terraform.Tests/ --filter "FullyQualifiedName~TerraformCliExecutorFixture" -v minimal
```

Expected: All tests pass (existing 8 + 8 new = 16).

- [ ] **Step 6: Commit**

```bash
git add source/Calamari.Terraform/Behaviours/TerraformDeployBehaviour.cs source/Calamari.Terraform.Tests/TerraformCliExecutorFixture.cs
git commit -m "test: add unit coverage for Terraform init command construction and version range checks"
```

---

### Task 8: Migrate the Terraform integration tests into `Calamari.ExternalTools.Tests`

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/TerraformCommandsFixture.cs`
- Move: `source/Calamari.Terraform.Tests/{AWS,Azure,GoogleCloud,PlanDetailedExitCode,Simple,WithOutputSensitiveVariables,WithVariablesSubstitution}/*` → `source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/{same}/*`
- Move: `source/Calamari.Terraform.Tests/CommonTemplates/SingleVariable.json` → `source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/CommonTemplates/SingleVariable.json`
- Delete: `source/Calamari.Terraform.Tests/CommandsFixture.cs`
- Delete: `source/Calamari.Terraform.Tests/{AdditionalParams,TemplateDirectory,WithVariables}/` (unused once `CommandsFixture.cs` is gone)
- Delete: `source/Calamari.Terraform.Tests/CommonTemplates/{HclWithVariables.hcl,InlineJsonWithVariables.json,TemplateLoader.cs}` (only `SingleVariable.json` is used by the migrated tests; the rest backed tests that were dropped, not migrated)

This keeps one end-to-end test, the wiring tests that catch pipeline/DI regressions unit tests can't, and the 3 cloud tests — matching what was already validated for this exact migration on `feature/external-tool-test-separation`. `Calamari.Terraform.Tests` (`CommandResolutionTests.cs`, `TerraformCliExecutorFixture.cs`, `TerraformPlanVariableFixture.cs`) stays in the solution and in the default pipeline.

**Interfaces:**
- Consumes: `ExternalToolFixture` (Task 6), `TerraformStrategy.Download` (Task 5), `TestEnvironment.GetTestPath` (`Calamari.Testing.Helpers`), `CommandTestBuilder<Calamari.Terraform.Program>` (`Calamari.Testing`, already used by the original fixture — no new dependency).

- [ ] **Step 1: Move the Terraform resource directories**

```bash
mkdir -p source/Calamari.ExternalTools.Tests/ExternalTools/Terraform
git mv source/Calamari.Terraform.Tests/AWS source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/AWS
git mv source/Calamari.Terraform.Tests/Azure source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/Azure
git mv source/Calamari.Terraform.Tests/GoogleCloud source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/GoogleCloud
git mv source/Calamari.Terraform.Tests/PlanDetailedExitCode source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/PlanDetailedExitCode
git mv source/Calamari.Terraform.Tests/Simple source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/Simple
git mv source/Calamari.Terraform.Tests/WithOutputSensitiveVariables source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/WithOutputSensitiveVariables
git mv source/Calamari.Terraform.Tests/WithVariablesSubstitution source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/WithVariablesSubstitution
mkdir -p source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/CommonTemplates
git mv source/Calamari.Terraform.Tests/CommonTemplates/SingleVariable.json source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/CommonTemplates/SingleVariable.json
```

- [ ] **Step 2: Delete resources that only backed dropped tests**

```bash
git rm -r source/Calamari.Terraform.Tests/AdditionalParams
git rm -r source/Calamari.Terraform.Tests/TemplateDirectory
git rm -r source/Calamari.Terraform.Tests/WithVariables
git rm source/Calamari.Terraform.Tests/CommonTemplates/HclWithVariables.hcl
git rm source/Calamari.Terraform.Tests/CommonTemplates/InlineJsonWithVariables.json
git rm source/Calamari.Terraform.Tests/CommonTemplates/TemplateLoader.cs
```

- [ ] **Step 3: Create the migrated fixture**

Create `source/Calamari.ExternalTools.Tests/ExternalTools/Terraform/TerraformCommandsFixture.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using Calamari.Terraform.Commands;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.ExternalTools.Terraform
{
    [TestFixture]
    [Category("ExternalTool")]
    public class TerraformCommandsFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "terraform";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => TerraformStrategy.Download(destinationDir, version, client);

        readonly string planCommand = GetCommandFromType(typeof(PlanCommand));
        readonly string applyCommand = GetCommandFromType(typeof(ApplyCommand));
        readonly string destroyCommand = GetCommandFromType(typeof(DestroyCommand));

        const string ResourceRoot = "ExternalTools/Terraform";

        static string GetTestResourcePath(string relativePath)
            => TestEnvironment.GetTestPath(ResourceRoot, relativePath);

        static string LoadTextTemplate(string templateName)
            => File.ReadAllText(GetTestResourcePath(Path.Combine("CommonTemplates", templateName)));

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            ClearTestDirectories();
        }

        static void ClearTestDirectories()
        {
            static void TryDeleteFile(string path)
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }

            static void TryDeleteDirectory(string path, bool recursive)
            {
                try { Directory.Delete(path, recursive); }
                catch (IOException) { }
            }

            static void ClearTerraformDirectory(string directory)
            {
                var fullPath = GetTestResourcePath(directory);
                TryDeleteFile(Path.Combine(fullPath, "terraform.tfstate"));
                TryDeleteFile(Path.Combine(fullPath, "terraform.tfstate.backup"));
                TryDeleteFile(Path.Combine(fullPath, "terraform.log"));
                TryDeleteDirectory(Path.Combine(fullPath, ".terraform"), true);
                TryDeleteDirectory(Path.Combine(fullPath, "terraform.tfstate.d"), true);
                TryDeleteDirectory(Path.Combine(fullPath, "terraformplugins"), true);
            }

            ClearTerraformDirectory("AWS");
            ClearTerraformDirectory("Azure");
            ClearTerraformDirectory("GoogleCloud");
            ClearTerraformDirectory("PlanDetailedExitCode");
            ClearTerraformDirectory("Simple");
            ClearTerraformDirectory("WithOutputSensitiveVariables");
            ClearTerraformDirectory("WithVariablesSubstitution");
        }

        /// <summary>Single end-to-end test validating the pipeline works with the manifest's Terraform version.</summary>
        [Test]
        public void ApplySimple_Succeeds()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { }, "Simple")
                .Should()
                .NotContain("Error");
        }

        [Test]
        public void InlineJsonTemplate_ProducesExpectedOutput()
        {
            string template = LoadTextTemplate("SingleVariable.json");
            var randomNumber = new Random().Next().ToString();

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", randomNumber);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{\"ami\":\"test-value\"}");
                                          _.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("test-value");
                                      });
        }

        /// <summary>Wiring test: Octostache substitution runs on variable files before terraform uses them.</summary>
        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.txt");
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "example.txt");
                                          _.Variables.Add("Octopus.Action.StepName", "Step Name");
                                          _.Variables.Add("Should_Be_Substituted", "Hello World");
                                          _.Variables.Add("Should_Be_Substituted_in_txt", "Hello World from text");
                                      },
                                      "WithVariablesSubstitution",
                                      result =>
                                      {
                                          result.OutputVariables["TerraformValueOutputs[my_output]"].Value.Should().Be("Hello World");
                                          result.OutputVariables["TerraformValueOutputs[my_output_from_txt_file]"].Value.Should().Be("Hello World from text");
                                      });
        }

        /// <summary>Wiring test: terraform's sensitive outputs are marked IsSensitive in Calamari's output variables.</summary>
        [Test]
        public void WithOutputSensitiveVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ => { },
                                      "WithOutputSensitiveVariables",
                                      result => result.OutputVariables.Values.Should().OnlyContain(variable => variable.IsSensitive));
        }

        /// <summary>Wiring test: plan -> apply -> plan cycle with state file management (exit code 2 = changes, 0 = no changes).</summary>
        [Test]
        public async Task PlanDetailedExitCode()
        {
            using var stateFileFolder = TemporaryDirectory.Create();

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("2");

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, "PlanDetailedExitCode");
            output.FullLog.Should().Contain("apply -auto-approve");

            output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("0");
            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams,
                                $"-state=\"{Path.Combine(stateFileFolder.DirectoryPath, "terraform.tfstate")}\" -refresh=false");
            }
        }

        [Test]
        public async Task GoogleCloudIntegration()
        {
            var bucketName = $"e2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(GetTestResourcePath("GoogleCloud"), temporaryFolder.DirectoryPath);

            var environmentJsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, CancellationToken.None);
            var jsonKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(environmentJsonKey));

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Hello", "Hello World from Google Cloud");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add("Octopus.Action.Terraform.GoogleCloudAccount", bool.TrueString);
                _.Variables.Add("Octopus.Action.GoogleCloudAccount.JsonKey", jsonKey);
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            var requestUri = output.OutputVariables["TerraformValueOutputs[url]"].Value;

            string fileData;
            var strategy = TestingRetryPolicies.CreateGoogleCloudHttpRetryPipeline();
            using (var client = new HttpClient())
            {
                var response = await strategy.ExecuteAsync(async _ => await client.GetAsync(requestUri));
                response.IsSuccessStatusCode.Should().BeTrue();
                fileData = await response.Content.ReadAsStringAsync();
            }

            fileData.Should().Be("Hello World from Google Cloud");

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            using (var client = new HttpClient())
            {
                var response = await strategy.ExecuteAsync(async _ => await client.GetAsync($"{requestUri}&bust_cache"));
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }

        [Test]
        public async Task AzureIntegration()
        {
            var resourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();
            var resourceGroupLocation = RandomAzureRegion.GetRandomRegionWithExclusions();

            var subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken.None);
            var tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken.None);
            var clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken.None);
            var clientPassword = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken.None);

            var random = Guid.NewGuid().ToString("N").Substring(0, 6);
            var appName = $"cfe2e-{random}";
            var expectedHostName = $"{appName}.azurewebsites.net";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(GetTestResourcePath("Azure"), temporaryFolder.DirectoryPath, ToolVersion);

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedHostName);
            await AssertRequestResponse(HttpStatusCode.Forbidden);

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            await AssertResponseIsNotReachable();
            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
                _.Variables.Add(AzureAccountVariables.TenantId, tenantId);
                _.Variables.Add(AzureAccountVariables.ClientId, clientId);
                _.Variables.Add(AzureAccountVariables.Password, clientPassword);
                _.Variables.Add("app_name", appName);
                _.Variables.Add("resource_group_name", resourceGroupName);
                _.Variables.Add("resource_group_location", resourceGroupLocation);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount, Boolean.TrueString);
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

            async Task AssertResponseIsNotReachable()
            {
                try
                {
                    await AssertRequestResponse(HttpStatusCode.NotFound);
                }
                catch (HttpRequestException ex)
                {
                    switch (ex.InnerException)
                    {
                        case SocketException socketException:
                            socketException.Message.Should().BeOneOf(
                                "No such host is known.", "Name or service not known", "nodename nor servname provided, or not known");
                            break;
                        case WebException webException:
                            webException.Message.Should().StartWith("The remote name could not be resolved");
                            break;
                        default:
                            throw;
                    }
                }
            }

            async Task AssertRequestResponse(HttpStatusCode expectedStatusCode)
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"https://{expectedHostName}").ConfigureAwait(false);
                response.StatusCode.Should().Be(expectedStatusCode);
            }
        }

        [Test]
        [Ignore("Test needs to be updated because s3 bucket doesn't seem to support ACLs anymore.")]
        public async Task AWSIntegration()
        {
            var bucketName = $"cfe2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var expectedUrl = $"https://{bucketName}.s3.amazonaws.com/test.txt";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(GetTestResourcePath("AWS"), temporaryFolder.DirectoryPath);

            var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
            var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedUrl);

            string fileData;
            using (var client = new HttpClient())
                fileData = await client.GetStringAsync(expectedUrl).ConfigureAwait(false);

            fileData.Should().Be("Hello World from AWS");

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(expectedUrl).ConfigureAwait(false);
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }

            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Octopus.Action.Amazon.AccessKey", accessKey);
                _.Variables.Add("Octopus.Action.Amazon.SecretKey", secretKey);
                _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Variables.Add("Hello", "Hello World from AWS");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }
        }

        static void CopyAllFiles(string sourceFolderPath, string destinationFolderPath, string? terraformVersion = null)
        {
            if (!Directory.Exists(sourceFolderPath))
                throw new Exception($"'{nameof(sourceFolderPath)}' ({sourceFolderPath}) does not exist!");

            if (terraformVersion != null && Directory.Exists(Path.Combine(sourceFolderPath, terraformVersion)))
                sourceFolderPath = Path.Combine(sourceFolderPath, terraformVersion);

            foreach (var filePath in Directory.GetFiles(sourceFolderPath))
            {
                var destFilePath = Path.Combine(destinationFolderPath, Path.GetFileName(filePath));
                File.Copy(filePath, destFilePath, true);
            }
        }

        string ExecuteAndReturnLogOutput(string command, Action<CommandTestBuilderContext> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
        {
            return ExecuteAndReturnResult(command, populateVariables, folderName, assert).Result.FullLog;
        }

        async Task<TestCalamariCommandResult> ExecuteAndReturnResult(string command, Action<CommandTestBuilderContext> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
        {
            var assertResult = assert ?? (_ => { });
            var terraformFiles = Path.IsPathRooted(folderName) ? folderName : GetTestResourcePath(folderName);

            var result = await CommandTestBuilder.CreateAsync<Calamari.Terraform.Program>(command)
                                                 .WithArrange(context =>
                                                              {
                                                                  context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Package);
                                                                  context.Variables.Add(TerraformSpecialVariables.Packages.PackageId, terraformFiles);
                                                                  context.Variables.Add(TerraformSpecialVariables.Calamari.TerraformCliPath, Path.GetDirectoryName(ToolExecutablePath));
                                                                  context.Variables.Add(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable, ToolExecutablePath);

                                                                  populateVariables(context);

                                                                  var isInline = context.Variables.Get(ScriptVariables.ScriptSource)!
                                                                                        .Equals(ScriptVariables.ScriptSourceOptions.Inline, StringComparison.InvariantCultureIgnoreCase);
                                                                  if (isInline)
                                                                  {
                                                                      var template = context.Variables.Get(TerraformSpecialVariables.Action.Terraform.Template);
                                                                      var templateParameters = context.Variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateParameters);
                                                                      var isJsonFormat = true;

                                                                      try { JToken.Parse(template); }
                                                                      catch { isJsonFormat = false; }

                                                                      context.WithDataFileNoBom(template!, isJsonFormat ? TerraformSpecialVariables.JsonTemplateFile : TerraformSpecialVariables.HclTemplateFile);
                                                                      context.WithDataFileNoBom(templateParameters!, isJsonFormat ? TerraformSpecialVariables.JsonVariablesFile : TerraformSpecialVariables.HclVariablesFile);
                                                                  }

                                                                  if (!String.IsNullOrEmpty(folderName))
                                                                      context.WithFilesToCopy(terraformFiles);
                                                              })
                                                 .Execute();

            assertResult(result);
            return result;
        }

        static string GetCommandFromType(Type commandType)
        {
            return commandType.CustomAttributes.Where(t => t.AttributeType == typeof(Calamari.Common.Commands.CommandAttribute))
                              .Select(c => c.ConstructorArguments.First().Value)
                              .Single()
                              ?.ToString()!;
        }
    }
}
```

- [ ] **Step 4: Verify the migrated project builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

Expected: Build succeeds. If `TerraformSpecialVariables` or other internal Terraform types are inaccessible, verify Task 1 Step 5's `InternalsVisibleTo` change is present.

- [ ] **Step 5: Verify `Calamari.Terraform.Tests` still builds after the deletions**

```bash
cd source && dotnet build Calamari.Terraform.Tests/Calamari.Terraform.Tests.csproj
```

Expected: Build succeeds — `CommandResolutionTests.cs`, `TerraformCliExecutorFixture.cs`, `TerraformPlanVariableFixture.cs` are unaffected by the deletions in this task.

- [ ] **Step 6: Run the non-cloud tests to validate the infrastructure works end-to-end**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~TerraformCommandsFixture.ApplySimple_Succeeds" -v normal
```

Expected: Terraform is downloaded per the manifest (or found on PATH), and the test passes. This is the first real exercise of the full resolve → download → run pipeline.

- [ ] **Step 7: Run the remaining non-cloud tests**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~TerraformCommandsFixture&TestCategory!=Cloud" -v normal
```

Expected: `ApplySimple_Succeeds`, `InlineJsonTemplate_ProducesExpectedOutput`, `OutputAndSubstituteOctopusVariables`, `WithOutputSensitiveVariables`, `PlanDetailedExitCode` all pass. `GoogleCloudIntegration` and `AzureIntegration` require cloud credentials (`ExternalVariables`) and are expected to fail/skip locally without them; `AWSIntegration` is `[Ignore]`d.

- [ ] **Step 8: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/ source/Calamari.Terraform.Tests/
git commit -m "feat: migrate Terraform integration tests to Calamari.ExternalTools.Tests"
```

---

### Future Tasks (not in this plan)

- Migrate Helm, kubectl, Azure CLI, GCloud, AWS CLI, aws-iam-authenticator, kubelogin tool tests (add their manifest entries, download strategies, and fixtures)
- Migrate Azure App Service, AzureResourceGroup, AzureWebApp, GoogleCloudScripting cloud tests into a new `CloudIntegration/` subdir
- Add a Nuke build target and TeamCity pipeline stage that runs `--filter "Category=ExternalTool"` on a nightly/on-demand schedule (the category attribute itself already exists on `TerraformCommandsFixture`, so no CI job currently isolates or runs it — it simply isn't excluded from a manual full-project `dotnet test` run either)
- Automated version-expansion scheduled job (`[Explicit]` `ToolVersionExpansionFixture`, `LatestVersionFinder`) that bumps `tool-manifest.json`'s `highest` automatically
- Add tar.gz extraction support to `ToolDownloader` when a tool that needs it (Helm, GCloud, kubelogin) is migrated
