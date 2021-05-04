using System;
using System.IO;
using System.IO.Compression;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Tests.Shared;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class BundledCliFixture
    {
        internal const string TerraformVersion = "0.11.15";

        //Note that the CLI package may not end up in the test build folder when running locally.
        [Test]
        [WindowsTest]
        public void BundledCliIsTheCorrectVersion()
        {
            using var tempDir = TemporaryDirectory.Create();
            ZipFile.ExtractToDirectory("Octopus.Dependencies.TerraformCLI.nupkg", tempDir.DirectoryPath);

            var terraformExePath = Path.Combine(tempDir.DirectoryPath,
                                                "contentFiles",
                                                "any",
                                                "win",
                                                "terraform.exe");
            var log = new InMemoryLog();
            var result = new CommandLineRunner(log, new CalamariVariables()).Execute(new CommandLineInvocation(terraformExePath, "--version"));
            result.ExitCode.Should().Be(0);

            log.StandardOut.Should().Contain($"Terraform v{TerraformVersion}");
        }
    }
}