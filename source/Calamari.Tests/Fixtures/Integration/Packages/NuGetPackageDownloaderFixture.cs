using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Calamari.Integration.Packages.NuGet;
using Calamari.Integration.Retry;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class NuGetPackageDownloaderFixture
    {
        [Test]
        public void AttemptsOnlyOnceIfSuccessful()
        {
            var packageId = "FakePackageId";
            var version = new NuGetVersion(1, 2, 3);
            var feedUri = new Uri("http://www.myget.org");
            var feedCredentials = new CredentialCache();
            var targetFilePath = "FakeTargetFilePath";

            var calledCount = 0;
            var downloader = new NuGetPackageDownloader();
            downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, (arg1, arg2, arg3, arg4, arg5) =>
            {
                calledCount++;
            });

            Assert.That(calledCount, Is.EqualTo(1));
        }

        [Test]
        public void AttemptsFiveTimesOnError()
        {
            var packageId = "FakePackageId";
            var version = new NuGetVersion(1, 2, 3);
            var feedUri = new Uri("http://www.myget.org");
            var feedCredentials = new CredentialCache();
            var targetFilePath = "FakeTargetFilePath";

            var calledCount = 0;
            Assert.Throws<Exception>(() =>
            {
                //total attempts is initial attempt + 4 retries
                var retryTracker = new RetryTracker(maxRetries: 4, 
                                                    timeLimit: null, 
                                                    retryInterval: new RetryInterval(100, 150, 1));
                var downloader = new NuGetPackageDownloader(retryTracker);
                downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath,
                    (arg1, arg2, arg3, arg4, arg5) =>
                    {
                        calledCount++;
                        throw new Exception("Expected exception from test");
                    });
            });
            Assert.That(calledCount, Is.EqualTo(5));
        }
    }
}
