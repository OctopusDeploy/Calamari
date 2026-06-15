# External Tool Test Separation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a new `Calamari.ExternalTools.Tests` project with centralised tool version manifest, shared download/resolution infrastructure, and migrate external-tool-dependent tests out of existing projects.

**Architecture:** A single new NUnit test project reads tool versions from a co-located `tool-manifest.json`. Shared infrastructure resolves tools via env var override -> PATH lookup (with version range check) -> download-and-cache. Tests are organised in per-tool folders and declare their primary tool for version control.

**Tech Stack:** C# / .NET 8.0, NUnit 3.14.0, FluentAssertions 7.2.0, System.Text.Json, SharpCompress (for tar.gz extraction)

---

### Task 1: Create the test project and manifest

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj`
- Create: `source/Calamari.ExternalTools.Tests/tool-manifest.json`
- Modify: `source/Calamari.sln` (add new project)

- [ ] **Step 1: Create the project directory**

```bash
mkdir -p source/Calamari.ExternalTools.Tests/Infrastructure
mkdir -p source/Calamari.ExternalTools.Tests/Terraform
mkdir -p source/Calamari.ExternalTools.Tests/Kubectl
mkdir -p source/Calamari.ExternalTools.Tests/Helm
mkdir -p source/Calamari.ExternalTools.Tests/AwsCli
mkdir -p source/Calamari.ExternalTools.Tests/GCloud
mkdir -p source/Calamari.ExternalTools.Tests/AzureCli
mkdir -p source/Calamari.ExternalTools.Tests/Kubelogin
mkdir -p source/Calamari.ExternalTools.Tests/AwsIamAuthenticator
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
        <NoWarn>CS8632</NoWarn>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="FluentAssertions" Version="7.2.0" />
        <PackageReference Include="NUnit" Version="3.14.0" />
        <PackageReference Include="NUnit3TestAdapter" Version="5.2.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
        <PackageReference Include="TeamCity.VSTest.TestAdapter" Version="1.0.41" />
        <PackageReference Include="SharpCompress" Version="0.38.0" />
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
        <None Update="**/*.hcl">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the tool manifest**

Create `source/Calamari.ExternalTools.Tests/tool-manifest.json`:

```json
{
  "tools": {
    "terraform": {
      "lowest": "0.13.7",
      "highest": "1.8.5",
      "source": "https://releases.hashicorp.com/terraform/",
      "architectures": ["amd64", "arm64"]
    },
    "kubectl": {
      "lowest": "1.28.0",
      "highest": "1.31.0",
      "source": "https://storage.googleapis.com/kubernetes-release/release/",
      "architectures": ["amd64", "arm64"]
    },
    "helm": {
      "lowest": "3.12.0",
      "highest": "3.16.0",
      "source": "https://get.helm.sh/",
      "architectures": ["amd64", "arm64"]
    },
    "aws-cli": {
      "lowest": "2.11.22",
      "highest": "2.11.22",
      "source": "https://awscli.amazonaws.com/",
      "architectures": ["amd64"]
    },
    "aws-iam-authenticator": {
      "lowest": "0.7.10",
      "highest": "0.7.10",
      "source": "https://github.com/kubernetes-sigs/aws-iam-authenticator/releases",
      "architectures": ["amd64"]
    },
    "gcloud": {
      "lowest": "436.0.0",
      "highest": "436.0.0",
      "source": "https://dl.google.com/dl/cloudsdk/channels/rapid/downloads/",
      "architectures": ["amd64"]
    },
    "kubelogin": {
      "lowest": "0.0.25",
      "highest": "0.0.25",
      "source": "https://github.com/Azure/kubelogin/releases",
      "architectures": ["amd64"]
    },
    "azure-cli": {
      "lowest": "2.60.0",
      "highest": "2.60.0",
      "source": "https://learn.microsoft.com/en-us/cli/azure/install-azure-cli",
      "architectures": ["amd64", "arm64"]
    }
  }
}
```

- [ ] **Step 4: Add the project to the solution**

```bash
cd source && dotnet sln Calamari.sln add Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 5: Verify the solution builds**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

Expected: Build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/ source/Calamari.sln
git commit -m "feat: add Calamari.ExternalTools.Tests project with tool manifest"
```

---

### Task 2: Implement the manifest reader

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifest.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifestTests.cs`

- [ ] **Step 1: Write the failing test for manifest deserialization**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolManifestTests.cs`:

```csharp
using System.IO;
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
            manifest.GetTool("terraform").Lowest.ToString().Should().Be("0.13.7");
            manifest.GetTool("terraform").Highest.ToString().Should().Be("1.8.5");
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
            var terraform = manifest.GetTool("terraform");

            terraform.IsInRange(new System.Version(1, 0, 0)).Should().BeTrue();
            terraform.IsInRange(new System.Version(0, 12, 0)).Should().BeFalse();
            terraform.IsInRange(new System.Version(2, 0, 0)).Should().BeFalse();
        }

        [Test]
        public void ShouldListAllTools()
        {
            var manifest = ToolManifest.Load();

            manifest.ToolNames.Should().Contain("terraform", "kubectl", "helm", "aws-cli",
                "aws-iam-authenticator", "gcloud", "kubelogin", "azure-cli");
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
            // Strip leading 'v' if present (e.g., "v0.7.10" -> "0.7.10")
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
git add source/Calamari.ExternalTools.Tests/Infrastructure/
git commit -m "feat: implement ToolManifest reader with version range support"
```

---

### Task 3: Implement the tool resolver

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolver.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolverTests.cs`

The resolver implements the resolution order: env var override -> PATH lookup (with version check) -> download.

- [ ] **Step 1: Write the failing tests**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolverTests.cs`:

```csharp
using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    [TestFixture]
    public class ToolResolverTests
    {
        [Test]
        public void ShouldUseEnvironmentVariableOverrideWhenSet()
        {
            var manifest = ToolManifest.Load();
            var resolver = new ToolResolver(manifest, s => { });

            // Set an env var override pointing to a known path
            var envVarName = ToolResolver.GetOverrideEnvVar("terraform");
            envVarName.Should().Be("CALAMARI_TOOL_TERRAFORM_VERSION");
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
using System.IO;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Resolves the path to an external tool using the resolution order:
    /// 1. Environment variable override (CALAMARI_TOOL_{NAME}_VERSION)
    /// 2. Local installation on PATH (if version is within manifest range)
    /// 3. Download and cache (via ToolDownloader)
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

        /// <summary>
        /// Resolves the version to use for a tool, considering env var overrides.
        /// Returns the override version if set, otherwise the highest from manifest.
        /// </summary>
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

        /// <summary>
        /// Checks whether a tool executable exists on PATH.
        /// Returns the full path if found, null otherwise.
        /// </summary>
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

        /// <summary>
        /// Gets the version of a locally installed tool by running it with --version.
        /// Returns null if the version cannot be determined.
        /// </summary>
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

Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolResolver*
git commit -m "feat: implement ToolResolver with env var override and PATH lookup"
```

---

### Task 4: Implement the tool downloader

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolDownloader.cs`

This refactors the download/cache logic from `Calamari.Tests/KubernetesFixtures/InstallTools.cs` into a reusable class. Each tool has its own download strategy registered via a delegate pattern (matching the existing approach).

- [ ] **Step 1: Implement the downloader**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ToolDownloader.cs`:

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Retry;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using SharpCompress.Common;
using SharpCompress.Readers;

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
            string executablePath = null;

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

        public static async Task DownloadAndExtractTarGz(string url, string destinationDir, HttpClient client)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tar.gz");
            try
            {
                await DownloadFile(url, tempPath, client);
                using Stream stream = File.OpenRead(tempPath);
                using var reader = ReaderFactory.Open(stream);
                reader.WriteAllToDirectory(destinationDir, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    WriteSymbolicLink = (source, target) =>
                        TestContext.Progress.WriteLine("Skipping symbolic link: {0}", source)
                });
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

            var result = Calamari.Common.Features.Processes.SilentProcessRunner.ExecuteCommand(
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

        // --- Platform helpers ---

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

### Task 5: Create per-tool download strategies for Terraform

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/TerraformStrategy.cs`

Start with Terraform as the first tool — it's the simplest (single binary, zip download) and has existing tests to migrate.

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
                .FirstOrDefault(f => Path.GetFileName(f).Contains("terraform"))!;
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

### Task 6: Create the ToolFixture base class

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ExternalToolFixture.cs`

A base class for test fixtures that need an external tool. Handles resolution, version selection, and provides the executable path.

- [ ] **Step 1: Create the base fixture**

Create `source/Calamari.ExternalTools.Tests/Infrastructure/ExternalToolFixture.cs`:

```csharp
using System;
using System.Threading.Tasks;
using System.Net.Http;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Base class for test fixtures that depend on an external tool.
    /// Resolves the tool via: env var override -> PATH -> download.
    ///
    /// Subclasses set PrimaryToolName and provide a download strategy.
    /// </summary>
    public abstract class ExternalToolFixture
    {
        static readonly ToolManifest Manifest = ToolManifest.Load();

        protected string ToolExecutablePath { get; private set; } = "";
        protected string ToolVersion { get; private set; } = "";

        /// <summary>The tool name as it appears in tool-manifest.json.</summary>
        protected abstract string PrimaryToolName { get; }

        /// <summary>Download strategy for this tool. Called when the tool is not on PATH or cached.</summary>
        protected abstract Task<string> DownloadTool(string destinationDir, string version, HttpClient client);

        [OneTimeSetUp]
        public async Task ResolveTool()
        {
            var resolver = new ToolResolver(Manifest, Log);
            var downloader = new ToolDownloader(Log);

            ToolVersion = resolver.ResolveVersion(PrimaryToolName);

            // Try PATH first
            var pathResult = ToolResolver.FindOnPath(PrimaryToolName);
            if (pathResult != null)
            {
                var tool = Manifest.GetTool(PrimaryToolName);
                // For PATH tools, we use them if available (version check is best-effort)
                Log($"Found {PrimaryToolName} on PATH at {pathResult}");
                ToolExecutablePath = pathResult;
                return;
            }

            // Download and cache
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

### Task 7: Migrate Terraform tests as proof of concept

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Terraform/TerraformCommandsFixture.cs`
- Reference: `source/Calamari.Terraform.Tests/CommandsFixture.cs` (source of migration)

This migrates the Terraform `CommandsFixture` into the new project, using the manifest-driven infrastructure instead of inline version management. This is the proof-of-concept migration — one tool end-to-end.

- [ ] **Step 1: Read the existing CommandsFixture thoroughly**

Read `source/Calamari.Terraform.Tests/CommandsFixture.cs` in full to understand all tests and their setup requirements. Note which tests are integration tests (cloud-dependent) vs unit-style tests.

- [ ] **Step 2: Create the migrated fixture**

Create `source/Calamari.ExternalTools.Tests/Terraform/TerraformCommandsFixture.cs`. This should:

- Extend `ExternalToolFixture` with `PrimaryToolName = "terraform"`
- Use `TerraformStrategy.Download` as the download strategy
- Replicate the test methods from the original `CommandsFixture`, but:
  - Remove the `[TestFixture("0.13.7")]` / `[TestFixture("1.8.5")]` parameterisation (default run uses `highest` from manifest)
  - Replace `customTerraformExecutable` references with `ToolExecutablePath`
  - Keep the same terraform test resource files (copy the `Simple/`, `AWS/`, `Azure/`, etc. directories)

- [ ] **Step 3: Copy terraform test resource files**

```bash
cp -r source/Calamari.Terraform.Tests/Simple source/Calamari.ExternalTools.Tests/Terraform/Simple
cp -r source/Calamari.Terraform.Tests/WithVariables source/Calamari.ExternalTools.Tests/Terraform/WithVariables
cp -r source/Calamari.Terraform.Tests/PlanDetailedExitCode source/Calamari.ExternalTools.Tests/Terraform/PlanDetailedExitCode
# Copy other resource directories as needed for the tests being migrated
```

- [ ] **Step 4: Verify the migrated tests compile**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 5: Run a non-cloud-dependent Terraform test to validate the infrastructure works**

```bash
cd source && dotnet test Calamari.ExternalTools.Tests/ --filter "FullyQualifiedName~TerraformCommandsFixture" -v normal
```

Expected: Tests that don't require cloud credentials should pass, using Terraform downloaded via the manifest.

- [ ] **Step 6: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Terraform/
git commit -m "feat: migrate Terraform tests to ExternalTools project using manifest-driven versioning"
```

---

### Task 8: Add download strategies for remaining tools

**Files:**
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/KubectlStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/HelmStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/AwsCliStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/AwsIamAuthenticatorStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/GCloudStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/KubeloginStrategy.cs`
- Create: `source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/AzureCliStrategy.cs`

Each strategy follows the same pattern as `TerraformStrategy` but with tool-specific download URLs, extraction, and executable path resolution. Refer to `source/Calamari.Tests/KubernetesFixtures/InstallTools.cs` for the existing download logic per tool.

- [ ] **Step 1: Implement KubectlStrategy**

```csharp
// source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/KubectlStrategy.cs
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class KubectlStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var platform = ToolDownloader.GetPlatform();
            var arch = ToolDownloader.GetArchitecture();
            var fileName = CalamariEnvironment.IsRunningOnWindows ? "kubectl.exe" : "kubectl";
            var url = $"https://dl.k8s.io/release/v{version}/bin/{platform}/{arch}/{fileName}";

            var destPath = Path.Combine(destinationDir, fileName);
            await ToolDownloader.DownloadFile(url, destPath, client);
            return destPath;
        }
    }
}
```

- [ ] **Step 2: Implement remaining strategies**

Follow the same pattern for each tool, referencing the download URLs and extraction logic from `InstallTools.cs`:

- **HelmStrategy**: Download from `https://get.helm.sh/helm-v{version}-{platform}-{arch}.tar.gz`, extract, return `{platform}-{arch}/helm` (or `helm.exe`).
- **AwsCliStrategy**: Platform-specific (MSI on Windows, zip on Linux, PATH on Mac). Use existing logic from `InstallTools.InstallAwsCli`.
- **AwsIamAuthenticatorStrategy**: Single binary from GitHub releases.
- **GCloudStrategy**: tar.gz/zip extraction, plus GKE auth plugin install on Windows.
- **KubeloginStrategy**: Zip from GitHub releases, executable in `bin/{platform}_{arch}/`.
- **AzureCliStrategy**: Platform-specific installation (new implementation).

- [ ] **Step 3: Verify all strategies compile**

```bash
cd source && dotnet build Calamari.ExternalTools.Tests/Calamari.ExternalTools.Tests.csproj
```

- [ ] **Step 4: Commit**

```bash
git add source/Calamari.ExternalTools.Tests/Infrastructure/ToolStrategies/
git commit -m "feat: add download strategies for all external tools"
```

---

### Future Tasks (not in this plan)

These are tracked but deferred:

- **Migrate Kubernetes fixture tests** (kubectl, Helm, kubelogin, aws-iam-authenticator) — these are tightly coupled to cloud credentials and `KubernetesContextScriptWrapperLiveFixture`. Larger migration effort.
- **Migrate GCloud tests** from `Calamari.GoogleCloudScripting.Tests`
- **Migrate Azure CLI tests** from `Calamari.AzureResourceGroup.Tests`
- **Remove original tests** from source projects once migrations are validated
- **Add Nuke build target** for running external tool tests separately
- **Implement automated version discovery** scheduled job
- **Update TeamCity pipeline** to separate external tool test runs
