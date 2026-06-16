using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.ExternalTools.Kubectl
{
    [TestFixture]
    public class KubectlFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "kubectl";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => KubectlStrategy.Download(destinationDir, version, client);

        [Test]
        public void ShouldResolveToValidExecutable()
        {
            File.Exists(ToolExecutablePath).Should().BeTrue(
                $"kubectl should exist at {ToolExecutablePath}");
        }

        [Test]
        public void ShouldReportVersion()
        {
            var versionOutput = ToolResolver.GetInstalledVersion(ToolExecutablePath, "version --client");
            versionOutput.Should().NotBeNullOrEmpty();
            versionOutput.Should().Contain("Client Version");
        }
    }
}
