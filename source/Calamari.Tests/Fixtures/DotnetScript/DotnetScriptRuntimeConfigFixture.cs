#nullable enable
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    /// <summary>
    /// dotnet-script is framework-dependent and the upstream build requests
    /// Microsoft.NETCore.App 8.0.0 with no rollForward, so by default it will not start on a target
    /// that has no 8.x runtime installed. Calamari ships a roll-forward policy over the top.
    ///
    /// The policy lives in source control as dotnet-script.runtimeconfig.json and is copied over the
    /// extracted file by IncludeDotNetScript.targets. These tests guard the two ways that can break:
    /// the copy silently not happening, and a future re-vendor bringing new upstream settings that
    /// the override then clobbers.
    /// </summary>
    [TestFixture]
    [Category(TestCategory.PlatformAgnostic)]
    public class DotnetScriptRuntimeConfigFixture
    {
        const string ExpectedRollForward = "Major";

        [Test]
        public void BundledDotnetScript_RollsForwardToTheNewestInstalledRuntime()
        {
            var runtimeConfig = JObject.Parse(File.ReadAllText(BundledRuntimeConfigPath()));

            runtimeConfig["runtimeOptions"]?["rollForward"]?.Value<string>()
                                                            .Should()
                                                            .Be(ExpectedRollForward,
                                                                "without it, C# script steps fail to launch on a target that has no 8.x runtime");
        }

        /// <summary>
        /// The override replaces the whole file, so anything upstream adds or changes would be
        /// silently dropped. If this fails after re-vendoring the zip, reconcile the override with
        /// the new upstream file rather than just updating the expectation.
        /// </summary>
        [Test]
        public void BundledRuntimeConfig_DiffersFromUpstreamOnlyByRollForward()
        {
            var vendoredZip = FindVendoredZip();
            if (vendoredZip == null)
                Assert.Inconclusive("Vendored dotnet-script zip not found - this test needs a source checkout.");

            using var archive = ZipFile.OpenRead(vendoredZip!);
            var upstreamEntry = archive.Entries
                                       .Single(e => e.FullName.EndsWith("dotnet-script.runtimeconfig.json"));

            using var reader = new StreamReader(upstreamEntry.Open());
            var upstream = JObject.Parse(reader.ReadToEnd());
            var shipped = JObject.Parse(File.ReadAllText(BundledRuntimeConfigPath()));

            upstream["runtimeOptions"]?["rollForward"]
                .Should()
                .BeNull("upstream is expected to set no policy - if it now does, the override may be redundant");

            // Normalise away the one intended difference, then the two files must agree.
            ((JObject)shipped["runtimeOptions"]!).Remove("rollForward");

            JToken.DeepEquals(shipped, upstream)
                  .Should()
                  .BeTrue($"the shipped runtimeconfig should match upstream apart from rollForward.{System.Environment.NewLine}"
                          + $"upstream: {upstream.ToString(Newtonsoft.Json.Formatting.None)}{System.Environment.NewLine}"
                          + $"shipped:  {shipped.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        static string BundledRuntimeConfigPath()
        {
            var path = TestEnvironment.GetTestPath("dotnet-script", "dotnet-script.runtimeconfig.json");
            File.Exists(path)
                .Should()
                .BeTrue($"IncludeDotNetScript.targets should have extracted dotnet-script to {path}");
            return path;
        }

        /// <summary>
        /// Walks up from the test output to the checkout, since the zip is a source artefact and is
        /// not copied to the build output.
        /// </summary>
        static string? FindVendoredZip()
        {
            var directory = new DirectoryInfo(TestEnvironment.CurrentWorkingDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "source", "Calamari.Scripting", "DotnetScript");
                if (Directory.Exists(candidate))
                    return Directory.GetFiles(candidate, "dotnet-script.*.zip").SingleOrDefault();

                directory = directory.Parent;
            }

            return null;
        }
    }
}
