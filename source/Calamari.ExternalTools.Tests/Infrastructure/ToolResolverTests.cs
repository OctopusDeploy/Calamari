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
