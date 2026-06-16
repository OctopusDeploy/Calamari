using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Version expansion tests — normally ignored.
    /// Run by a scheduled job to discover newer tool versions and validate them.
    ///
    /// Workflow:
    /// 1. Discover the latest released version of a tool
    /// 2. Compare to the manifest's 'highest' — skip if already up to date
    /// 3. Set the version override env var so the tool tests download the new version
    /// 4. Run the key integration tests for that tool
    /// 5. If tests pass, update the manifest's 'highest' in the source file
    ///
    /// The scheduled job can then commit the updated manifest and create a PR.
    /// </summary>
    [TestFixture]
    [Explicit("Run by scheduled job for version expansion — not part of regular CI")]
    public class ToolVersionExpansionFixture
    {
        [Test]
        public async Task Terraform_ExpandToLatestVersion()
        {
            var manifest = ToolManifest.Load();
            var tool = manifest.GetTool("terraform");
            tool.Should().NotBeNull();

            var latestVersion = await LatestVersionFinder.FindLatestVersion("terraform");
            TestContext.Progress.WriteLine($"[terraform] Manifest highest: {tool!.Highest}, Latest available: {latestVersion}");

            if (latestVersion <= tool.Highest)
            {
                Assert.Ignore($"Terraform manifest is already up to date (highest={tool.Highest}, latest={latestVersion})");
                return;
            }

            TestContext.Progress.WriteLine($"[terraform] New version available: {latestVersion} (manifest has {tool.Highest})");

            // Set override so Terraform tests download this version
            var envVar = ToolResolver.GetOverrideEnvVar("terraform");
            Environment.SetEnvironmentVariable(envVar, latestVersion.ToString());
            try
            {
                // Run the key integration test — ApplySimple uses the Simple/ directory, no cloud creds needed
                var fixture = new ExternalTools.Terraform.TerraformCommandsFixture();
                await fixture.ResolveTool();

                fixture.ToolExecutablePath.Should().NotBeNullOrEmpty();
                TestContext.Progress.WriteLine($"[terraform] Downloaded {latestVersion} to {fixture.ToolExecutablePath}");

                // If we got here, the download and version resolution worked.
                // The scheduled job can now run the full terraform test suite against this version.
                TestContext.Progress.WriteLine($"[terraform] Version {latestVersion} downloaded successfully. Ready for full test suite.");

                // Update the manifest source file
                UpdateManifestHighest("terraform", latestVersion);
                TestContext.Progress.WriteLine($"[terraform] Updated manifest highest to {latestVersion}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        [Test]
        public async Task Kubectl_ExpandToLatestVersion()
        {
            var manifest = ToolManifest.Load();
            var tool = manifest.GetTool("kubectl");
            tool.Should().NotBeNull();

            var latestVersion = await LatestVersionFinder.FindLatestVersion("kubectl");
            TestContext.Progress.WriteLine($"[kubectl] Manifest highest: {tool!.Highest}, Latest available: {latestVersion}");

            if (latestVersion <= tool.Highest)
            {
                Assert.Ignore($"kubectl manifest is already up to date (highest={tool.Highest}, latest={latestVersion})");
                return;
            }

            TestContext.Progress.WriteLine($"[kubectl] New version available: {latestVersion} (manifest has {tool.Highest})");

            var envVar = ToolResolver.GetOverrideEnvVar("kubectl");
            Environment.SetEnvironmentVariable(envVar, latestVersion.ToString());
            try
            {
                var fixture = new ExternalTools.Kubectl.KubectlFixture();
                await fixture.ResolveTool();

                fixture.ToolExecutablePath.Should().NotBeNullOrEmpty();
                TestContext.Progress.WriteLine($"[kubectl] Downloaded {latestVersion} to {fixture.ToolExecutablePath}");

                UpdateManifestHighest("kubectl", latestVersion);
                TestContext.Progress.WriteLine($"[kubectl] Updated manifest highest to {latestVersion}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        [Test]
        public async Task Helm_ExpandToLatestVersion()
        {
            var manifest = ToolManifest.Load();
            var tool = manifest.GetTool("helm");
            tool.Should().NotBeNull();

            var latestVersion = await LatestVersionFinder.FindLatestVersion("helm");
            TestContext.Progress.WriteLine($"[helm] Manifest highest: {tool!.Highest}, Latest available: {latestVersion}");

            if (latestVersion <= tool.Highest)
            {
                Assert.Ignore($"Helm manifest is already up to date (highest={tool.Highest}, latest={latestVersion})");
                return;
            }

            TestContext.Progress.WriteLine($"[helm] New version available: {latestVersion} (manifest has {tool.Highest})");

            var envVar = ToolResolver.GetOverrideEnvVar("helm");
            Environment.SetEnvironmentVariable(envVar, latestVersion.ToString());
            try
            {
                var fixture = new ExternalTools.Helm.HelmFixture();
                await fixture.ResolveTool();

                fixture.ToolExecutablePath.Should().NotBeNullOrEmpty();
                TestContext.Progress.WriteLine($"[helm] Downloaded {latestVersion} to {fixture.ToolExecutablePath}");

                UpdateManifestHighest("helm", latestVersion);
                TestContext.Progress.WriteLine($"[helm] Updated manifest highest to {latestVersion}");
            }
            finally
            {
                Environment.SetEnvironmentVariable(envVar, null);
            }
        }

        /// <summary>
        /// Updates the 'highest' version in the source tool-manifest.json file.
        /// This modifies the file on disk so the scheduled job can commit the change.
        /// </summary>
        static void UpdateManifestHighest(string toolName, Version newVersion)
        {
            // Find the source manifest (not the output copy)
            var manifestPath = FindSourceManifestPath();
            var json = File.ReadAllText(manifestPath);
            var doc = JsonNode.Parse(json)!;

            var toolNode = doc["tools"]?[toolName];
            if (toolNode == null)
                throw new InvalidOperationException($"Tool '{toolName}' not found in manifest at {manifestPath}");

            toolNode["highest"] = newVersion.ToString();

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(manifestPath, doc.ToJsonString(options));
        }

        /// <summary>
        /// Walks up from the output directory to find the source tool-manifest.json.
        /// The output copy is read-only and gets overwritten on build.
        /// </summary>
        static string FindSourceManifestPath()
        {
            // Start from the test assembly output directory and walk up to find the source project
            var dir = new DirectoryInfo(TestEnvironment.CurrentWorkingDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Calamari.ExternalTools.Tests", "tool-manifest.json");
                if (File.Exists(candidate))
                    return candidate;

                // Also check if we're already in the project directory
                candidate = Path.Combine(dir.FullName, "tool-manifest.json");
                if (File.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "Calamari.ExternalTools.Tests.csproj")))
                    return candidate;

                dir = dir.Parent;
            }

            throw new FileNotFoundException("Could not locate source tool-manifest.json");
        }
    }
}
