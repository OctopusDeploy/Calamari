using System;
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
