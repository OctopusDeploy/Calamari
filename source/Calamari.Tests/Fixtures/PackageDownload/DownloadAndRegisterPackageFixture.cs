using System;
using System.Globalization;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class DownloadAndRegisterPackageFixture : CalamariFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "DownloadAndRegisterPackage");
        static readonly string DownloadPath = TestEnvironment.GetTestPath(TentacleHome, "Files");

        static readonly string PublicFeedUri = "https://f.feedz.io/octopus-deploy/integration-tests/nuget/index.json";
        static readonly string ExpectedPackageHash = "1e0856338eb5ada3b30903b980cef9892ebf7201";
        static readonly long ExpectedPackageSize = 3749;
        static readonly SampleFeedPackage FeedzPackage = new SampleFeedPackage()
        {
            Id = "feeds-feedz",
            Version = new SemanticVersion("1.0.0"),
            PackageId = "OctoConsole"
        };

        static readonly string MavenPublicFeedUri = "https://repo.maven.apache.org/maven2/";
        static readonly string ExpectedMavenPackageHash = "3564ef3803de51fb0530a8377ec6100b33b0d073";
        static readonly long ExpectedMavenPackageSize = 2575022;
        static readonly SampleFeedPackage MavenPublicFeed = new SampleFeedPackage("#")
        {
            Id = "feeds-maven",
            Version = VersionFactory.CreateMavenVersion("22.0"),
            PackageId = "com.google.guava:guava"
        };

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
            if (Directory.Exists(DownloadPath))
                Directory.Delete(DownloadPath, true);
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        public void ShouldDownloadAndRegisterPackage()
        {
            var result = DownloadAndRegisterPackage(
                FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id,
                PublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Downloading package {0} v{1}...", FeedzPackage.PackageId, FeedzPackage.Version);
            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'",
                FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'",
                FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);

            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        public void ShouldDownloadAndRegisterMavenPackage()
        {
            var result = DownloadAndRegisterPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            result.AssertSuccess();

            result.AssertOutput("Downloading package {0} v{1}...", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            result.AssertOutput("Downloading Maven package {0} v{1} from feed: '{2}'",
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'",
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);

            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed);
        }

        [Test]
        public void ShouldSetOutputVariables()
        {
            var result = DownloadAndRegisterPackage(
                FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id,
                PublicFeedUri);

            result.AssertSuccess();

            // Verify all expected output variables are set
            result.AssertOutputVariable("StagedPackage.Hash", Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable("StagedPackage.Size",
                Is.EqualTo(ExpectedPackageSize.ToString(CultureInfo.InvariantCulture)));
            result.AssertOutputVariable("StagedPackage.FullPathOnRemoteMachine",
                Does.Match(PackageName.ToRegexPattern(FeedzPackage.PackageId, FeedzPackage.Version, FeedzPackage.DownloadFolder) + ".*"));
        }

        [Test]
        public void ShouldUsePackageFromCacheAndStillRegister()
        {
            // First download
            var firstResult = DownloadAndRegisterPackage(
                FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id,
                PublicFeedUri);

            firstResult.AssertSuccess();

            // Second download should use cache but still register
            var secondResult = DownloadAndRegisterPackage(
                FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id,
                PublicFeedUri);

            secondResult.AssertSuccess();

            secondResult.AssertOutput("Checking package cache for package {0} v{1}",
                FeedzPackage.PackageId, FeedzPackage.Version);
            secondResult.AssertOutputMatches(string.Format(
                "Package was found in cache\\. No need to download. Using file: '{0}'",
                PackageName.ToRegexPattern(FeedzPackage.PackageId, FeedzPackage.Version, FeedzPackage.DownloadFolder)));

            AssertPackageHashMatchesExpected(secondResult, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(secondResult, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(secondResult, FeedzPackage);
        }

        [Test]
        public void ShouldFailWhenTaskIdNotProvided()
        {
            var calamari = Calamari()
                .Action("download-and-register-package")
                .Argument("packageId", FeedzPackage.PackageId)
                .Argument("packageVersion", FeedzPackage.Version.ToString())
                .Argument("packageVersionFormat", VersionFormat.Semver)
                .Argument("feedId", FeedzPackage.Id)
                .Argument("feedUri", PublicFeedUri)
                .Argument("feedType", FeedType.NuGet);
                // Don't provide taskId argument

            var result = Invoke(calamari);

            result.AssertFailure();
        }

        CalamariResult DownloadAndRegisterPackage(
            string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername = "",
            string feedPassword = "",
            FeedType feedType = FeedType.NuGet,
            VersionFormat versionFormat = VersionFormat.Semver,
            bool forcePackageDownload = false,
            int attempts = 5,
            int attemptBackoffSeconds = 0)
        {
            var calamari = Calamari()
                .Action("download-and-register-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
                .Argument("taskId", "ServerTasks-12345")
                .Argument("packageVersionFormat", versionFormat)
                .Argument("feedId", feedId)
                .Argument("feedUri", feedUri)
                .Argument("feedType", feedType)
                .Argument("attempts", attempts.ToString())
                .Argument("attemptBackoffSeconds", attemptBackoffSeconds.ToString());

            if (!String.IsNullOrWhiteSpace(feedUsername))
                calamari.Argument("feedUsername", feedUsername);

            if (!String.IsNullOrWhiteSpace(feedPassword))
                calamari.Argument("feedPassword", feedPassword);

            if (forcePackageDownload)
                calamari.Flag("forcePackageDownload");

            return Invoke(calamari);
        }

        static void AssertPackageHashMatchesExpected(CalamariResult result, string expectedHash)
        {
            result.AssertOutputVariable("StagedPackage.Hash", Is.EqualTo(expectedHash));
        }

        static void AssertPackageSizeMatchesExpected(CalamariResult result, long expectedSize)
        {
            result.AssertOutputVariable("StagedPackage.Size",
                Is.EqualTo(expectedSize.ToString(CultureInfo.InvariantCulture)));
        }

        static void AssertStagePackageOutputVariableSet(CalamariResult result, SampleFeedPackage testFeed)
        {
            var newPackageRegex = PackageName.ToRegexPattern(testFeed.PackageId, testFeed.Version, testFeed.DownloadFolder);
            result.AssertOutputVariable("StagedPackage.FullPathOnRemoteMachine", Does.Match(newPackageRegex + ".*"));
        }

        class SampleFeedPackage
        {
            public SampleFeedPackage()
            {
                Delimiter = ".";
            }

            public SampleFeedPackage(string delimiter)
            {
                Delimiter = delimiter;
            }

            private string Delimiter { get; set; }

            public string Id { get; set; }

            public string PackageId { get; set; }

            public IVersion Version { get; set; }

            public string DownloadFolder => Path.Combine(DownloadPath, Id);
        }
    }
}
