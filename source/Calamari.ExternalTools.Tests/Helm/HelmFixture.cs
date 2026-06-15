using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Helm
{
    [TestFixture]
    public class HelmFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "helm";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => HelmStrategy.Download(destinationDir, version, client);

        [Test]
        public void ShouldResolveToValidExecutable()
        {
            File.Exists(ToolExecutablePath).Should().BeTrue(
                $"helm should exist at {ToolExecutablePath}");
        }

        [Test]
        public void ShouldReportVersion()
        {
            var versionOutput = ToolResolver.GetInstalledVersion(ToolExecutablePath, "version --short");
            versionOutput.Should().NotBeNullOrEmpty();
            versionOutput.Should().StartWith("v");
        }
    }
}
