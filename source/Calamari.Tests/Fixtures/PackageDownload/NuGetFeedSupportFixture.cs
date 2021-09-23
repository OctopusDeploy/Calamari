using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class NuGetFeedVersionSupportFixture : CalamariFixture
    {
        const string PackageId = "Calamari.Tests.Fixtures.PackageDownload.NuGetFeedSupport";
        
        // TODO: Packages here were generated using the nuspec file in the .\NuGetFeedSupport folder
        // Right now, they have been manually uploaded to the feedz.io repository below.
        // In future, we should ensure this test fixture sets its own data up from scratch before running
        // and tears it down on completion, rather than relying on external state as it currently does.
        const string NuGetV2FeedUrl = "https://f.feedz.io/octopus-deploy/integration-tests/nuget";
        const string NuGetV3FeedUrl = "https://f.feedz.io/octopus-deploy/integration-tests/nuget/index.json";
        
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(TentacleHome))
                Directory.CreateDirectory(TentacleHome);

            Directory.SetCurrentDirectory(TentacleHome);

            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
            Console.WriteLine("TentacleHome is set to: " + TentacleHome);
        }

        [TearDown]
        public void TearDown()
        {
            var downloadPath = TestEnvironment.GetTestPath(TentacleHome, "Files");
            
            if (Directory.Exists(downloadPath))
                Directory.Delete(downloadPath, true);
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }
        
        [Test]
        [TestCaseSource(nameof(NuGet2SupportedVersionStrings))]
        public void ShouldSupportNuGetVersion2Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(PackageId, versionString, "nuget-local", NuGetV2FeedUrl);
            
            calamariResult.AssertSuccess();
        }
        
        [Test]
        [TestCaseSource(nameof(NuGet3SupportedVersionStrings))]
        public void ShouldNotSupportSemVer2OnNuGetVersion2Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(PackageId, versionString, "nuget-local", NuGetV2FeedUrl);
            
            calamariResult.AssertFailure();
            calamariResult.AssertOutput($"Could not find package Calamari.Tests.Fixtures.PackageDownload.NuGetFeedSupport {versionString} in feed: '{NuGetV2FeedUrl}'");
        }

        [Test]
        [TestCaseSource(nameof(NuGet2SupportedVersionStrings))]
        [TestCaseSource(nameof(NuGet3SupportedVersionStrings))]
        public void ShouldSupportNuGetVersion3Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(PackageId, versionString, "nuget-local", NuGetV3FeedUrl);
            
            calamariResult.AssertSuccess();
        }

        public static IEnumerable<TestCaseData> NuGet2SupportedVersionStrings
        {
            get
            {
                yield return new TestCaseData("0.0.1").SetDescription("Pre-v1 Patch Version");
                yield return new TestCaseData("0.5.1").SetDescription("Pre-v1 Minor Version");
                yield return new TestCaseData("1.0.0");
                yield return new TestCaseData("1.4.92").SetDescription("Multi-digit Patch Version");
                yield return new TestCaseData("1.101.0").SetDescription("Multi-digit Minor Version");
                yield return new TestCaseData("101.0.0").SetDescription("Multi-digit Major Version");
                yield return new TestCaseData("2.0.0-alpha").SetDescription("SemVer 1.0 alpha pre-release");
                yield return new TestCaseData("2.0.0-beta").SetDescription("SemVer 1.0 beta pre-release");
            }
        }

        public static IEnumerable<TestCaseData> NuGet3SupportedVersionStrings
        {
            get
            {
                yield return new TestCaseData("2.0.0-beta.2").SetDescription("Pre-release version with dot suffix");
                yield return new TestCaseData("2.0.0-beta.2.1").SetDescription("Pre-release version with double-dot suffix");
                yield return new TestCaseData("2.0.0-beta+abcd16bd").SetDescription("Pre-release version with metadata");
                yield return new TestCaseData("2.0.0-beta.1+abcd16bd").SetDescription("Pre-release version with dot suffix and metadata");
            }
        }
        
        CalamariResult DownloadPackage(string packageId,
                                       string packageVersion,
                                       string feedId,
                                       string feedUri,
                                       string feedUsername = "",
                                       string feedPassword = "")
        {
            var calamari = Calamari()
                           .Action("download-package")
                           .Argument("packageId", packageId)
                           .Argument("packageVersion", packageVersion)
                           .Argument("packageVersionFormat", VersionFormat.Semver)
                           .Argument("feedId", feedId)
                           .Argument("feedUri", feedUri)
                           .Argument("feedType", FeedType.NuGet)
                           .Argument("attempts", 1)
                           .Argument("attemptBackoffSeconds", 0);

            if (!string.IsNullOrWhiteSpace(feedUsername))
                calamari.Argument("feedUsername", feedUsername);

            if (!string.IsNullOrWhiteSpace(feedPassword))
                calamari.Argument("feedPassword", feedPassword);

            return Invoke(calamari);
        }
    }
}