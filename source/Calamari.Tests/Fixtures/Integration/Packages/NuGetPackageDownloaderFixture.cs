using System;
using System.Diagnostics;
using System.Net;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.NuGet;
using Calamari.Testing.Requirements;
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
        const string SkipFreeBsdBecause = "performance on Mono+FreeBSD fluctuates significantly";

        // We only support the specification of HTTP timeouts on V3 nuget endpoints in
        // .NET framework. V2 nuget endpoints and .net core runtimes execute entirely
        // different codepaths that don't give us an easy way to allow users to specify 
        // timeouts.

        [Test]
        [NonParallelizable]
        [RequiresNonFreeBSDPlatform(SkipFreeBsdBecause)]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void TimesOutIfAValidTimeoutIsDefinedInVariables()
        {
            RunNugetV3TimeoutTest("00:00:01", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        }

        [Test]
        [NonParallelizable]
        [RequiresNonFreeBSDPlatform(SkipFreeBsdBecause)]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void IgnoresTheTimeoutIfAnInvalidTimeoutIsDefinedInVariables()
        {
            RunNugetV3TimeoutTest("this is not a valid timespan", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        [Test]
        [NonParallelizable]
        [RequiresNonFreeBSDPlatform(SkipFreeBsdBecause)]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void DoesNotTimeOutIfTheServerRespondsBeforeTheTimeout()
        {
            RunNugetV3TimeoutTest("00:01:00", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        void RunNugetV3TimeoutTest(string timeoutInVariables, TimeSpan serverResponseTime, TimeSpan estimatedTimeout)
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
                    variables[KnownVariables.NugetHttpTimeout] = timeoutInVariables;
                }
                
                var downloader = new InternalNuGetPackageDownloader(filesystem, variables);

                var stopwatch = new Stopwatch();
                
                Action invocation = () =>
                {
                    stopwatch.Start();
                    try
                    {
                        downloader.DownloadPackage(
                            packageId,
                            version,
                            v3NugetUri,
                            feedCredentials,
                            targetFilePath,
                            maxDownloadAttempts: 1,
                            downloadAttemptBackoff: TimeSpan.Zero
                        );
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
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
