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
