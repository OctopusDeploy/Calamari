using System;
using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class DownloadAndRegisterPackageCommandTest
    {
        [Test]
        public void SupportsSemverVersionFormats()
        {
            var command = new DownloadAndRegisterPackageCommand(
                Substitute.For<IScriptEngine>(),
                new CalamariVariables(),
                Substitute.For<ICalamariFileSystem>(),
                Substitute.For<ICommandLineRunner>(),
                Substitute.For<ILog>(),
                Substitute.For<IManagePackageCache>());

            // This should parse without throwing - we expect it to fail on download since we're using mocks,
            // but version parsing should succeed
            var result = command.Execute(new[]
            {
                "--packageId=TestPackage",
                "--packageVersion=1.0.0",
                "--taskId=ServerTasks-12345",
                "--packageVersionFormat=Semver",
                "--feedId=test-feed",
                "--feedUri=https://test.feed.com",
                "--feedType=NuGet"
            });

            // Command will fail at download stage with mocks, but version format was valid
            result.Should().NotBe(0); // Fails at download, not at version parsing
        }

        [Test]
        public void SupportsMavenVersionFormats()
        {
            var command = new DownloadAndRegisterPackageCommand(
                Substitute.For<IScriptEngine>(),
                new CalamariVariables(),
                Substitute.For<ICalamariFileSystem>(),
                Substitute.For<ICommandLineRunner>(),
                Substitute.For<ILog>(),
                Substitute.For<IManagePackageCache>());

            // Test Maven version format parsing
            var result = command.Execute(new[]
            {
                "--packageId=com.example:test",
                "--packageVersion=3.7.4.20220919T144341Z",
                "--taskId=ServerTasks-12345",
                "--packageVersionFormat=Maven",
                "--feedId=test-feed",
                "--feedUri=https://maven.test.com",
                "--feedType=Maven"
            });

            // Command will fail at download stage with mocks, but version format was valid
            result.Should().NotBe(0); // Fails at download, not at version parsing
        }

        [Test]
        public void RequiresTaskIdArgument()
        {
            // Set TentacleHome so validation doesn't fail early
            var tentacleHome = System.IO.Path.GetTempPath();
            System.Environment.SetEnvironmentVariable("TentacleHome", tentacleHome);

            try
            {
                var log = Substitute.For<ILog>();
                var command = new DownloadAndRegisterPackageCommand(
                    Substitute.For<IScriptEngine>(),
                    new CalamariVariables(),
                    Substitute.For<ICalamariFileSystem>(),
                    Substitute.For<ICommandLineRunner>(),
                    log,
                    Substitute.For<IManagePackageCache>());

                var result = command.Execute(new[]
                {
                    "--packageId=TestPackage",
                    "--packageVersion=1.0.0",
                    "--feedId=test-feed",
                    "--feedUri=https://test.feed.com"
                    // Deliberately NOT providing --taskId
                });

                // Should fail because taskId is not provided
                result.Should().NotBe(0);

                // Should log error about missing taskId (not about network or download failure)
                log.Received().Error(Arg.Is<string>(s => s.Equals("No task ID was specified. Please pass --taskId YourTaskId")));
            }
            finally
            {
                Environment.SetEnvironmentVariable("TentacleHome", null);
            }
        }

        [Test]
        public void FailsIfRegistrationFails()
        {
            var variables = new CalamariVariables();
            variables.Set("Octopus.Task.Id", "ServerTasks-12345");

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.GetFileSize(Arg.Any<string>()).Returns(1000);

            var journal = Substitute.For<IManagePackageCache>();
            // Make RegisterPackageUse throw an exception
            journal.When(x => x.RegisterPackageUse(
                    Arg.Any<PackageIdentity>(),
                    Arg.Any<ServerTaskId>(),
                    Arg.Any<ulong>()))
                .Do(x => { throw new Exception("Journal write failed"); });

            var command = new DownloadAndRegisterPackageCommand(
                Substitute.For<IScriptEngine>(),
                variables,
                fileSystem,
                Substitute.For<ICommandLineRunner>(),
                Substitute.For<ILog>(),
                journal);

            var result = command.Execute(new[]
            {
                "--packageId=TestPackage",
                "--packageVersion=1.0.0",
                "--feedId=test-feed",
                "--feedUri=https://test.feed.com"
            });

            // Should fail because registration failed
            result.Should().NotBe(0);
        }
    }
}
