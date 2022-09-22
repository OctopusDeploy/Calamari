using System;
using System.Globalization;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class PackageDownloadFixture : CalamariFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");
        static readonly string DownloadPath = TestEnvironment.GetTestPath(TentacleHome, "Files");

        static readonly string PublicFeedUri = "https://f.feedz.io/octopus-deploy/integration-tests/nuget/index.json";
        static readonly string NuGetFeedUri = "https://www.nuget.org/api/v2/";

        private static readonly string AuthFeedUri = ExternalVariables.Get(ExternalVariable.MyGetFeedUrl);
        private static readonly string AuthFeedUsername = ExternalVariables.Get(ExternalVariable.MyGetFeedUsername);
        private static readonly string AuthFeedPassword = ExternalVariables.Get(ExternalVariable.MyGetFeedPassword);
        static readonly string ExpectedPackageHash = "1e0856338eb5ada3b30903b980cef9892ebf7201";

        static readonly long ExpectedPackageSize = 3749;
        static readonly SampleFeedPackage FeedzPackage = new SampleFeedPackage() { Id = "feeds-feedz", Version = new SemanticVersion("1.0.0"), PackageId = "OctoConsole" };
        static readonly SampleFeedPackage FileShare = new SampleFeedPackage() { Id = "feeds-local", Version = new SemanticVersion(1, 0, 0), PackageId = "Acme.Web" };
        static readonly SampleFeedPackage NuGetFeed = new SampleFeedPackage() { Id = "feeds-nuget", Version = new SemanticVersion(2, 1, 0), PackageId = "abp.castle.log4net" };

        static readonly string ExpectedMavenPackageHash = "3564ef3803de51fb0530a8377ec6100b33b0d073";
        static readonly long ExpectedMavenPackageSize = 2575022;
        static readonly string MavenPublicFeedUri = "https://repo.maven.apache.org/maven2/";
        static readonly SampleFeedPackage MavenPublicFeed = new SampleFeedPackage("#") { Id = "feeds-maven", Version = VersionFactory.CreateMavenVersion("22.0"), PackageId = "com.google.guava:guava" };

       static readonly string MavenSnapshotPublicFeedUri = "https://oss.sonatype.org/content/repositories/snapshots/";
        // If this version is no longer available go to https://oss.sonatype.org/#view-repositories;releases~browseindex
        // In the bottom panel, expand Releases -> com -> google -> guava and find whatever the latest Snapshot version is
        // Also update the hash and size
        static readonly SampleFeedPackage MavenSnapshotPublicFeed = new SampleFeedPackage("#") { Id = "feeds-maven", Version = VersionFactory.CreateMavenVersion("24.0-SNAPSHOT"), PackageId = "com.google.guava:guava" };
        static readonly string ExpectedMavenSnapshotPackageHash = "425932eacd1450c4c4c32b9ed8a1d9b397f20082";
        static readonly long ExpectedMavenSnapshotPackageSize = 2621686;

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
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void ShouldDownloadPackage()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, PublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", FeedzPackage.DownloadFolder);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldDownloadMavenPackage()
        {
            if (CalamariEnvironment.IsRunningOnMac && TestEnvironment.IsCI && !CalamariEnvironment.IsRunningOnMono)
                Assert.Inconclusive("As of November 2018, this test is failing under dotnet core on the cloudmac under teamcity - we were getting an error 'SSL connect error' when trying to download from  'https://repo.maven.apache.org/maven2/'. Marking as inconclusive so we can re-enable the build - it had been disabled for months :(");

            var result = DownloadPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            result.AssertSuccess();

            result.AssertOutput("Downloading Maven package {0} v{1} from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", MavenPublicFeed.DownloadFolder);
            result.AssertOutput("Found package {0} v{1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);

            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'",
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldDownloadMavenSnapshotPackage()
        {
            if (CalamariEnvironment.IsRunningOnMac && TestEnvironment.IsCI && !CalamariEnvironment.IsRunningOnMono)
                Assert.Inconclusive("As of November 2018, this test is failing under dotnet core on the cloudmac under teamcity - we were getting an error 'SSL connect error' when trying to download from  'https://repo.maven.apache.org/maven2/'. Marking as inconclusive so we can re-enable the build - it had been disabled for months :(");

            var result = DownloadPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            result.AssertSuccess();

            result.AssertOutput("Downloading Maven package {0} v{1} from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", MavenPublicFeed.DownloadFolder);
            result.AssertOutput("Found package {0} v{1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);

            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'",
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void ShouldDownloadPackageWithRepositoryMetadata()
        {
            var result = DownloadPackage(NuGetFeed.PackageId, NuGetFeed.Version.ToString(), NuGetFeed.Id, NuGetFeedUri);

            result.AssertSuccess();

            result.AssertOutput(
                $"Downloading NuGet package {NuGetFeed.PackageId} v{NuGetFeed.Version} from feed: '{NuGetFeedUri}'");
            result.AssertOutput($"Downloaded package will be stored in: '{NuGetFeed.DownloadFolder}");

            AssertStagePackageOutputVariableSet(result, NuGetFeed);
            result.AssertOutput($"Package {NuGetFeed.PackageId} v{NuGetFeed.Version} successfully downloaded from feed: '{NuGetFeedUri}'");
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void ShouldUsePackageFromCache()
        {
            DownloadPackage(FeedzPackage.PackageId,
                    FeedzPackage.Version.ToString(),
                    FeedzPackage.Id,
                    PublicFeedUri)
                .AssertSuccess();

            var result = DownloadPackage(FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id,
                PublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} v{1}", FeedzPackage.PackageId, FeedzPackage.Version);
            result.AssertOutputMatches(string.Format("Package was found in cache\\. No need to download. Using file: '{0}'", PackageName.ToRegexPattern(FeedzPackage.PackageId, FeedzPackage.Version, FeedzPackage.DownloadFolder)));
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldUseMavenPackageFromCache()
        {
            if (CalamariEnvironment.IsRunningOnMac && TestEnvironment.IsCI && !CalamariEnvironment.IsRunningOnMono)
                Assert.Inconclusive("As of November 2018, this test is failing under dotnet core on the cloudmac under teamcity - we were getting an error 'SSL connect error' when trying to download from  'https://repo.maven.apache.org/maven2/'. Marking as inconclusive so we can re-enable the build - it had been disabled for months :(");

            DownloadPackage(MavenPublicFeed.PackageId,
                    MavenPublicFeed.Version.ToString(),
                    MavenPublicFeed.Id,
                    MavenPublicFeedUri,
                    feedType: FeedType.Maven,
                    versionFormat: VersionFormat.Maven)
                .AssertSuccess();

            var result = DownloadPackage(MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} v{1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            result.AssertOutputMatches(string.Format("Package was found in cache\\. No need to download. Using file: '{0}'", PackageName.ToRegexPattern(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.DownloadFolder)));
            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldUseMavenSnapshotPackageFromCache()
        {
            if (CalamariEnvironment.IsRunningOnMac && TestEnvironment.IsCI && !CalamariEnvironment.IsRunningOnMono)
                Assert.Inconclusive("As of November 2018, this test is failing under dotnet core on the cloudmac under teamcity - we were getting an error 'SSL connect error' when trying to download from  'https://repo.maven.apache.org/maven2/'. Marking as inconclusive so we can re-enable the build - it had been disabled for months :(");

            DownloadPackage(MavenPublicFeed.PackageId,
                    MavenPublicFeed.Version.ToString(),
                    MavenPublicFeed.Id,
                    MavenPublicFeedUri,
                    feedType: FeedType.Maven,
                    versionFormat: VersionFormat.Maven)
                .AssertSuccess();

            var result = DownloadPackage(MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id, MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} v{1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            result.AssertOutputMatches($"Package was found in cache\\. No need to download. Using file: '{PackageName.ToRegexPattern(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.DownloadFolder)}'");
            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void ShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id, PublicFeedUri).AssertSuccess();

            var result = DownloadPackage(FeedzPackage.PackageId,
                FeedzPackage.Version.ToString(),
                FeedzPackage.Id, PublicFeedUri,
                forcePackageDownload: true);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", FeedzPackage.DownloadFolder);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, PublicFeedUri);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldByPassCacheAndDownloadMavenPackage()
        {
            if (CalamariEnvironment.IsRunningOnMac && TestEnvironment.IsCI && !CalamariEnvironment.IsRunningOnMono)
                Assert.Inconclusive("As of November 2018, this test is failing under dotnet core on the cloudmac under teamcity - we were getting an error 'SSL connect error' when trying to download from  'https://repo.maven.apache.org/maven2/'. Marking as inconclusive so we can re-enable the build - it had been disabled for months :(");

            var firstDownload = DownloadPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                versionFormat: VersionFormat.Maven,
                feedType: FeedType.Maven);

            firstDownload.AssertSuccess();

            var secondDownload = DownloadPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version.ToString(),
                MavenPublicFeed.Id,
                MavenPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven,
                forcePackageDownload: true);

            secondDownload.AssertSuccess();

            secondDownload.AssertOutput("Downloading Maven package {0} v{1} from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            secondDownload.AssertOutput("Downloaded package will be stored in: '{0}'", MavenPublicFeed.DownloadFolder);
            secondDownload.AssertOutput("Found package {0} v{1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            AssertPackageHashMatchesExpected(secondDownload, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(secondDownload, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(secondDownload, MavenPublicFeed);
            secondDownload.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }

        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void ShouldByPassCacheAndDownloadMavenSnapshotPackage()
        {

            var firstDownload = DownloadPackage(
                MavenSnapshotPublicFeed.PackageId,
                MavenSnapshotPublicFeed.Version.ToString(),
                MavenSnapshotPublicFeed.Id,
                MavenSnapshotPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven);

            firstDownload.AssertSuccess();

            var secondDownload = DownloadPackage(
                MavenSnapshotPublicFeed.PackageId,
                MavenSnapshotPublicFeed.Version.ToString(),
                MavenSnapshotPublicFeed.Id,
                MavenSnapshotPublicFeedUri,
                feedType: FeedType.Maven,
                versionFormat: VersionFormat.Maven,
                forcePackageDownload: true);

            secondDownload.AssertSuccess();

            secondDownload.AssertOutput("Downloading Maven package {0} v{1} from feed: '{2}'", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version, MavenSnapshotPublicFeedUri);
            secondDownload.AssertOutput("Downloaded package will be stored in: '{0}'", MavenSnapshotPublicFeed.DownloadFolder);
            secondDownload.AssertOutput("Found package {0} v{1}", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version);
            AssertPackageHashMatchesExpected(secondDownload, ExpectedMavenSnapshotPackageHash);
            AssertPackageSizeMatchesExpected(secondDownload, ExpectedMavenSnapshotPackageSize);
            AssertStagePackageOutputVariableSet(secondDownload, MavenSnapshotPublicFeed);
            secondDownload.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version, MavenSnapshotPublicFeedUri);
        }

        [Test]
        [Ignore("Auth Feed Failing On Mono")]
        public void PrivateNuGetFeedShouldDownloadPackage()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, AuthFeedUsername, AuthFeedPassword);

            result.AssertSuccess();
            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", FeedzPackage.DownloadFolder);
#if USE_NUGET_V2_LIBS
            result.AssertOutput("Found package {0} v{1}", FeedzPackage.PackageId, FeedzPackage.Version);
#endif
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, AuthFeedUri);
        }

        [Test]
        [Ignore("Auth Feed Failing On Mono")]
        public void PrivateNuGetFeedShouldUsePackageFromCache()
        {
            DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, AuthFeedUsername, AuthFeedPassword)
                .AssertSuccess();

            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, AuthFeedUsername, AuthFeedPassword);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} v{1}", FeedzPackage.PackageId, FeedzPackage.Version);
            result.AssertOutputMatches($"Package was found in cache\\. No need to download. Using file: '{PackageName.ToRegexPattern(FeedzPackage.PackageId, FeedzPackage.Version, FeedzPackage.DownloadFolder)}'");
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
        }

        [Test]
        [Ignore("Auth Feed Failing On Mono")]
        public void PrivateNuGetFeedShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, AuthFeedUsername, AuthFeedPassword).AssertSuccess();

            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, AuthFeedUsername, AuthFeedPassword, forcePackageDownload: true);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", FeedzPackage.DownloadFolder);
#if USE_NUGET_V2_LIBS
            result.AssertOutput("Found package {0} v{1}", FeedzPackage.PackageId, FeedzPackage.Version);
#endif
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, FeedzPackage);
            result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, AuthFeedUri);
        }

        [Test]
        [Ignore("Auth Feed Failing On Mono")]
        public void PrivateNuGetFeedShouldFailDownloadPackageWhenInvalidCredentials()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, AuthFeedUri, "fake-feed-username", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            result.AssertFailure();

            result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FeedzPackage.PackageId, FeedzPackage.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", FeedzPackage.DownloadFolder);
            result.AssertErrorOutput("Unable to download package: The remote server returned an error: (401) Unauthorized.");
        }

        [Test]
        public void FileShareFeedShouldDownloadPackage()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                var result = DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, acmeWeb.DirectoryPath);

                result.AssertSuccess();

                result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}'", FileShare.DownloadFolder);
                result.AssertOutput("Found package {0} v{1}", FileShare.PackageId, FileShare.Version);
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare);
                result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FileShare.PackageId, FileShare.Version, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldUsePackageFromCache()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, acmeWeb.DirectoryPath).AssertSuccess();

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, acmeWeb.DirectoryPath);
                result.AssertSuccess();

                result.AssertOutput("Checking package cache for package {0} v{1}", FileShare.PackageId, FileShare.Version);
                result.AssertOutputMatches($"Package was found in cache\\. No need to download. Using file: '{PackageName.ToRegexPattern(FileShare.PackageId, FileShare.Version, FileShare.DownloadFolder)}'");
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare);
            }
        }

        [Test]
        public void FileShareFeedShouldByPassCacheAndDownloadPackage()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, acmeWeb.DirectoryPath)
                    .AssertSuccess();

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, acmeWeb.DirectoryPath,
                    forcePackageDownload: true);

                result.AssertSuccess();

                result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}'", FileShare.DownloadFolder);
                result.AssertOutput("Found package {0} v{1}", FileShare.PackageId, FileShare.Version);
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare);
                result.AssertOutput("Package {0} v{1} successfully downloaded from feed: '{2}'", FileShare.PackageId, FileShare.Version, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldFailDownloadPackageWhenInvalidFileShare()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                var invalidFileShareUri = new Uri(Path.Combine(acmeWeb.DirectoryPath, "InvalidPath"));

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version.ToString(), FileShare.Id, invalidFileShareUri.ToString());
                result.AssertFailure();

                result.AssertOutput("Downloading NuGet package {0} v{1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, invalidFileShareUri);
                result.AssertErrorOutput("Failed to download package Acme.Web v1.0.0 from feed: '{0}'", invalidFileShareUri);
                result.AssertErrorOutput("Failed to download package {0} v{1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, invalidFileShareUri);
            }
        }

        [Test]
        public void ShouldFailWhenNoPackageId()
        {
            var result = DownloadPackage("", FeedzPackage.Version.ToString(), FeedzPackage.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No package ID was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageId()
        {
            var invalidPackageId = string.Format("X{0}X", FeedzPackage.PackageId);
            var result = DownloadPackage(invalidPackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("Failed to download package {0} v{1} from feed: '{2}'", invalidPackageId, FeedzPackage.Version, PublicFeedUri);
        }

        [Test]
        public void ShouldFailWhenNoFeedVersion()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, "", FeedzPackage.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No package version was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedVersion()
        {
            const string invalidFeedVersion = "1.0.x";
            var result = DownloadPackage(FeedzPackage.PackageId, invalidFeedVersion, FeedzPackage.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("Package version '{0}' specified is not a valid Semver version string", invalidFeedVersion);
        }

        [Test]
        public void ShouldFailWhenNoFeedId()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), "", PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No feed ID was specified");
        }

        [Test]
        public void ShouldFailWhenNoFeedUri()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, "");
            result.AssertFailure();

            result.AssertErrorOutput("No feed URI was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedUri()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, "www.myget.org/F/octopusdeploy-tests");
            result.AssertFailure();

            result.AssertErrorOutput("URI specified 'www.myget.org/F/octopusdeploy-tests' is not a valid URI");
        }

        [Test]
        [Ignore("for now, runs fine locally...not sure why it's not failing in TC, will investigate")]
        public void ShouldFailWhenUsernameIsSpecifiedButNoPassword()
        {
            var result = DownloadPackage(FeedzPackage.PackageId, FeedzPackage.Version.ToString(), FeedzPackage.Id, PublicFeedUri, AuthFeedUsername);
            result.AssertFailure();

            result.AssertErrorOutput("A username was specified but no password was provided");
        }

        CalamariResult DownloadPackage(string packageId,
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
                .Action("download-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
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
            result.AssertOutputVariable("StagedPackage.Size", Is.EqualTo(expectedSize.ToString(CultureInfo.InvariantCulture)));
        }

        static void AssertStagePackageOutputVariableSet(CalamariResult result, SampleFeedPackage testFeed)
        {
            var newPacakgeRegex = PackageName.ToRegexPattern(testFeed.PackageId, testFeed.Version, testFeed.DownloadFolder);
            result.AssertOutputVariable("StagedPackage.FullPathOnRemoteMachine", Does.Match(newPacakgeRegex + ".*"));
        }

        private TemporaryFile CreateSamplePackage()
        {
            return new TemporaryFile(PackageBuilder.BuildSamplePackage(FileShare.PackageId, FileShare.Version.ToString()));
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
