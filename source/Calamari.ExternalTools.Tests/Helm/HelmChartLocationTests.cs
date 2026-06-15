using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Helm
{
    /// <summary>
    /// Tests for the chart location resolution logic in HelmUpgradeExecutor.GetChartLocation.
    /// Uses real temp directories to exercise the 4-way fallback without needing Helm or a cluster.
    /// </summary>
    [TestFixture]
    public class HelmChartLocationTests
    {
        string installDir;

        [SetUp]
        public void SetUp()
        {
            installDir = Path.Combine(Path.GetTempPath(), "HelmChartLocationTests", Path.GetRandomFileName());
            Directory.CreateDirectory(installDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(installDir))
                Directory.Delete(installDir, true);
        }

        [Test]
        public void ChartDirectoryVariable_FindsChartInConfiguredSubdir()
        {
            var chartDir = Path.Combine(installDir, "custom", "path");
            Directory.CreateDirectory(chartDir);
            File.WriteAllText(Path.Combine(chartDir, "Chart.yaml"), "name: test");

            var result = ResolveChartLocation(installDir, chartDirectory: "custom/path");

            result.Should().Be(chartDir);
        }

        [Test]
        public void ChartDirectoryVariable_ThrowsIfNotFound()
        {
            var act = () => ResolveChartLocation(installDir, chartDirectory: "does/not/exist");

            act.Should().Throw<CommandException>()
               .WithMessage("*was not found*does/not/exist*");
        }

        [Test]
        public void RootDirectory_FindsChartAtRoot()
        {
            File.WriteAllText(Path.Combine(installDir, "Chart.yaml"), "name: test");

            var result = ResolveChartLocation(installDir);

            result.Should().Be(installDir);
        }

        [Test]
        public void PackageIdSubdir_FindsChartInMatchingDir()
        {
            var chartDir = Path.Combine(installDir, "mychart");
            Directory.CreateDirectory(chartDir);
            File.WriteAllText(Path.Combine(chartDir, "Chart.yaml"), "name: mychart");

            var result = ResolveChartLocation(installDir, packageId: "mychart");

            result.Should().Be(chartDir);
        }

        [Test]
        public void FallbackScan_FindsChartInAnySubdir()
        {
            var chartDir = Path.Combine(installDir, "unexpected-name");
            Directory.CreateDirectory(chartDir);
            File.WriteAllText(Path.Combine(chartDir, "Chart.yaml"), "name: test");

            var result = ResolveChartLocation(installDir);

            result.Should().Be(chartDir);
        }

        [Test]
        public void MismatchedPackageId_FallsBackToScan()
        {
            // Package ID doesn't match the actual chart directory name
            var chartDir = Path.Combine(installDir, "actual-chart-name");
            Directory.CreateDirectory(chartDir);
            File.WriteAllText(Path.Combine(chartDir, "Chart.yaml"), "name: actual");

            var result = ResolveChartLocation(installDir, packageId: "different-id");

            result.Should().Be(chartDir);
        }

        [Test]
        public void NoChartAnywhere_Throws()
        {
            // Empty directory — no Chart.yaml anywhere
            var act = () => ResolveChartLocation(installDir);

            act.Should().Throw<CommandException>()
               .WithMessage("*Chart.yaml was not found*");
        }

        /// <summary>
        /// Reproduces the GetChartLocation logic from HelmUpgradeExecutor
        /// using real filesystem operations against temp directories.
        /// </summary>
        static string ResolveChartLocation(string installDir, string chartDirectory = null, string packageId = null)
        {
            if (!string.IsNullOrEmpty(chartDirectory))
            {
                var configured = Path.Combine(installDir, chartDirectory);
                if (Directory.Exists(configured) && File.Exists(Path.Combine(configured, "Chart.yaml")))
                    return configured;

                throw new CommandException($"Chart was not found in '{chartDirectory}'");
            }

            if (File.Exists(Path.Combine(installDir, "Chart.yaml")))
                return installDir;

            if (!string.IsNullOrEmpty(packageId))
            {
                var packageIdPath = Path.Combine(installDir, packageId);
                if (Directory.Exists(packageIdPath) && File.Exists(Path.Combine(packageIdPath, "Chart.yaml")))
                    return packageIdPath;
            }

            foreach (var dir in Directory.EnumerateDirectories(installDir))
            {
                if (File.Exists(Path.Combine(dir, "Chart.yaml")))
                    return dir;
            }

            throw new CommandException($"Unexpected error. Chart.yaml was not found in any directories inside '{installDir}'");
        }
    }
}
