using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    
    public class NuGetFeedVersionSupportFixture : CalamariFixture
    {
        const string FeedzPackageId = "Calamari.Tests.Fixtures.PackageDownload.NuGetFeedSupport";
        const string ArtifactoryPackageId = "Artifactory.Test.NuGet";
        
        // TODO: Packages here were generated using the nuspec file in the .\NuGetFeedSupport folder
        // Right now, they have been manually uploaded to the feedz.io repository below.
        // In future, we should ensure this test fixture sets its own data up from scratch before running
        // and tears it down on completion, rather than relying on external state as it currently does.
        const string FeedzNuGetV2FeedUrl = "https://f.feedz.io/octopus-deploy/integration-tests/nuget";
        const string FeedzNuGetV3FeedUrl = "https://f.feedz.io/octopus-deploy/integration-tests/nuget/index.json";
        const string ArtifactoryNuGetV2FeedUrl = "https://nuget.packages.octopushq.com/";
        const string ArtifactoryNuGetV3FeedUrl = "https://packages.octopushq.com/artifactory/api/nuget/v3/nuget";
        
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
        [TestCaseSource(nameof(FeedzNuGet2SupportedVersionStrings))]
        public void ShouldSupportFeedzNuGetVersion2Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(FeedzPackageId, versionString, "nuget-local", FeedzNuGetV2FeedUrl);
            calamariResult.AssertSuccess();
        }
        
        [Test]
        [TestCaseSource(nameof(FeedzNuGet2SupportedVersionStrings))]
        [TestCaseSource(nameof(FeedzNuGet3SupportedVersionStrings))]
        public void ShouldSupportFeedzNuGetVersion3Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(FeedzPackageId, versionString, "nuget-local", FeedzNuGetV3FeedUrl);
            
            calamariResult.AssertSuccess();
        }
        
        [Test]
        [TestCaseSource(nameof(ArtifactoryNuGet3SupportedVersionStrings))]
        [TestCaseSource(nameof(ArtifactoryNuGet2SupportedVersionStrings))]
        [Platform("Net-4.5")]
        public void ArtifactoryShouldSupportNuGetVersion3Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(ArtifactoryPackageId,  versionString, "nuget-local", ArtifactoryNuGetV3FeedUrl);
            calamariResult.AssertSuccess();
        }
        
        [Test]
        [TestCaseSource(nameof(ArtifactoryNuGet2SupportedVersionStrings))]
        public void ArtifactoryShouldSupportNuGetVersion2Feeds(string versionString)
        {
            var calamariResult = DownloadPackage(ArtifactoryPackageId,  versionString, "nuget-local", ArtifactoryNuGetV2FeedUrl);
            calamariResult.AssertSuccess();
        }

       

        public static IEnumerable<TestCaseData> FeedzNuGet2SupportedVersionStrings
        {
            get
            {
                yield return new TestCaseData("1.0.0");
                yield return new TestCaseData("1.4.92").SetDescription("Multi-digit Patch Version");
                yield return new TestCaseData("2.0.0-beta").SetDescription("SemVer 1.0 beta pre-release");
            }
        }

        public static IEnumerable<TestCaseData> FeedzNuGet3SupportedVersionStrings
        {
            get
            {

                yield return new TestCaseData("2.0.0-beta+abcd16bd").SetDescription("Pre-release version with metadata");
                yield return new TestCaseData("2.0.0-beta.1+abcd16bd").SetDescription("Pre-release version with dot suffix and metadata");
                yield return new TestCaseData("2.0.0-beta.2").SetDescription("Pre-release version with dot suffix");
                yield return new TestCaseData("2.0.0-beta.2.1").SetDescription("Pre-release version with double-dot suffix");
            }
        }
        public static IEnumerable<TestCaseData> ArtifactoryNuGet2SupportedVersionStrings
        {
            get
            {
                yield return new TestCaseData("1.0.0").SetDescription("Pre-release version with metadata");
            }
        }

        public static IEnumerable<TestCaseData> ArtifactoryNuGet3SupportedVersionStrings
        {
            get
            {
                yield return new TestCaseData("1.0.0-alpha.3+metadata").SetDescription("Pre-release version with dot suffix and metadata");
            }
        }
        
        CalamariResult DownloadPackage(string packageId, string packageVersion, string feedId, string feedUri, string feedUsername = "", string feedPassword = "")
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