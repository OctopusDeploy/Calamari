using System;
using System.Diagnostics;
using System.Net;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.NuGet;
using FluentAssertions;
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

#if USE_NUGET_V2_LIBS
        [Test]
        public void TimesOutIfAValidTimeoutIsDefinedInVariables()
        {
            RunTimeoutTest("00:00:01", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void IgnoresTheTimeoutIfAnInvalidTimeoutIsDefinedInVariables()
        {
            RunTimeoutTest("this is not a valid timespan", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        [Test]
        public void DoesNotTimeOutIfNoTimeoutIsDefinedInVariables()
        {
            RunTimeoutTest(null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        [Test]
        public void DoesNotTimeOutIfTheServerRespondsBeforeTheTimeout()
        {
            RunTimeoutTest("00:01:00", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        void RunTimeoutTest(string timeoutInVariables, TimeSpan serverResponseTime, TimeSpan estimatedTimeout)
        {
            using (var server = new TestHttpServer(9001, serverResponseTime))
            {
                var packageId = "FakePackageId";
                var version = VersionFactory.CreateSemanticVersion(1, 2, 3);
                var feedCredentials = new CredentialCache();
                var targetFilePath = "FakeTargetFilePath";
                var filesystem = Substitute.For<ICalamariFileSystem>();
                var v3NugetUri = new Uri(server.BaseUrl + "/index.json");
                var variables = new CalamariVariables();

                if (timeoutInVariables != null)
                {
                    variables[KnownVariables.NetfxNugetHttpTimeout] = timeoutInVariables;
                }
                
                var downloader = new InternalNuGetPackageDownloader(filesystem, variables);

                var stopwatch = new Stopwatch();
                
                Action invocation = () =>
                {
                    stopwatch.Start();
                    downloader.DownloadPackage(
                        packageId,
                        version,
                        v3NugetUri,
                        feedCredentials,
                        targetFilePath,
                        maxDownloadAttempts: 1,
                        downloadAttemptBackoff: TimeSpan.Zero
                    );
                    stopwatch.Stop();
                };

                invocation.Should()
                          .ThrowExactly<Exception>();

                stopwatch.Elapsed
                    .Should()
                    .BeCloseTo(estimatedTimeout, TimeSpan.FromSeconds(0.5));
            }
        }
#endif
    }
}
