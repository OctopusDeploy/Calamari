using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class FindAndRegisterPackageCommandTest
    {
        [Test]
        public void SupportsSemverVersionFormats()
        {
            var command = new FindAndRegisterPackageCommand(
                Substitute.For<ILog>(),
                Substitute.For<IPackageStore>(),
                Substitute.For<IManagePackageCache>(),
                Substitute.For<ICalamariFileSystem>());

            // Version parsing should succeed (command will fail at find stage with mocks)
            var result = command.Execute(new[]
            {
                "--packageId=TestPackage",
                "--packageVersion=1.0.0",
                "--taskId=ServerTasks-12345",
                "--packageVersionFormat=Semver",
                "--packageHash=abc123"
            });

            // Command will fail at find stage with mocks, but version format was valid
            result.Should().Be(0); // Returns 0 even when package not found (matches FindPackageCommand behavior)
        }

        [Test]
        public void SupportsMavenVersionFormats()
        {
            var command = new FindAndRegisterPackageCommand(
                Substitute.For<ILog>(),
                Substitute.For<IPackageStore>(),
                Substitute.For<IManagePackageCache>(),
                Substitute.For<ICalamariFileSystem>());

            // Test Maven version format parsing
            var result = command.Execute(new[]
            {
                "--packageId=com.example:test",
                "--packageVersion=3.7.4.20220919T144341Z",
                "--taskId=ServerTasks-12345",
                "--packageVersionFormat=Maven",
                "--packageHash=abc123"
            });

            // Command will fail at find stage with mocks, but version format was valid
            result.Should().Be(0); // Returns 0 even when package not found (matches FindPackageCommand behavior)
        }

        [Test]
        [Timeout(5000)] // Fail fast if validation is missing
        public void RequiresTaskIdArgument()
        {
            var log = Substitute.For<ILog>();
            var command = new FindAndRegisterPackageCommand(
                log,
                Substitute.For<IPackageStore>(),
                Substitute.For<IManagePackageCache>(),
                Substitute.For<ICalamariFileSystem>());

            var result = command.Execute(new[]
            {
                "--packageId=TestPackage",
                "--packageVersion=1.0.0",
                "--packageHash=abc123"
                // Deliberately NOT providing --taskId
            });

            // Should fail because taskId is not provided
            result.Should().NotBe(0);

            // Should fail because taskId is not provided
            log.Received().Error(Arg.Is<string>(s => s.Equals("No task ID was specified. Please pass --taskId YourTaskId")));
        }

        [Test]
        public void FailsIfRegistrationFails()
        {
            var packageStore = Substitute.For<IPackageStore>();
            // Return a mock package so we reach the registration step
            var version = Octopus.Versioning.VersionFactory.CreateSemanticVersion("1.0.0");
            var metadata = new PackageFileNameMetadata("TestPackage", version, version, ".nupkg");
            packageStore.GetPackage(Arg.Any<string>(), Arg.Any<Octopus.Versioning.IVersion>(), Arg.Any<string>())
                .Returns(new PackagePhysicalFileMetadata(metadata, "/test/path.nupkg", "abc123", 1000));

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.GetFileSize(Arg.Any<string>()).Returns(1000);

            var journal = Substitute.For<IManagePackageCache>();
            // Make RegisterPackageUse throw an exception
            journal.When(x => x.RegisterPackageUse(
                    Arg.Any<PackageIdentity>(),
                    Arg.Any<ServerTaskId>(),
                    Arg.Any<ulong>()))
                .Do(x => { throw new System.Exception("Journal write failed"); });

            var command = new FindAndRegisterPackageCommand(
                Substitute.For<ILog>(),
                packageStore,
                journal,
                fileSystem);

            var result = command.Execute(new[]
            {
                "--packageId=TestPackage",
                "--packageVersion=1.0.0",
                "--taskId=ServerTasks-12345",
                "--packageHash=abc123"
            });

            // Should fail because registration failed
            result.Should().NotBe(0);
        }
    }
}
