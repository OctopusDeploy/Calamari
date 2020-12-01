using System;
using System.Net;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.NuGet;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class NuGetPackageDownloaderFixture
    {
        [Test]
        public void AttemptsOnlyOnceIfSuccessful()
        {
            var packageId = "FakePackageId";
            var version = VersionFactory.CreateSemanticVersion(1, 2, 3);
            var feedUri = new Uri("http://www.myget.org");
            var feedCredentials = new CredentialCache();
            var targetFilePath = "FakeTargetFilePath";
            var filesystem = Substitute.For<ICalamariFileSystem>();
            var variables = new CalamariVariables();

            var calledCount = 0;
            var downloader = new InternalNuGetPackageDownloader(filesystem, variables);
            downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, maxDownloadAttempts: 5, downloadAttemptBackoff: TimeSpan.Zero, action: (arg1, arg2, arg3, arg4, arg5) =>
            {
                calledCount++;
            });

            Assert.That(calledCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(1, ExpectedResult = 1)]
        [TestCase(5, ExpectedResult = 5)]
        [TestCase(7, ExpectedResult = 7)]
        public int AttemptsTheRightNumberOfTimesOnError(int maxDownloadAttempts)
        {
            var packageId = "FakePackageId";
            var version = VersionFactory.CreateSemanticVersion(1, 2, 3);
            var feedUri = new Uri("http://www.myget.org");
            var feedCredentials = new CredentialCache();
            var targetFilePath = "FakeTargetFilePath";
            var filesystem = Substitute.For<ICalamariFileSystem>();
            var variables = new CalamariVariables();

            var calledCount = 0;
            Assert.Throws<Exception>(() =>
            {
                var downloader = new InternalNuGetPackageDownloader(filesystem, variables);
                downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, maxDownloadAttempts: maxDownloadAttempts, downloadAttemptBackoff: TimeSpan.Zero,
                    action: (arg1, arg2, arg3, arg4, arg5) =>
                    {
                        calledCount++;
                        throw new Exception("Expected exception from test: simulate download failing");
                    });
            });

            return calledCount;
        }
    }
}
