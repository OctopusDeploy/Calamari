using System;
using System.Globalization;
using System.IO;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class PackageDownloadFixture : CalamariFixture
    {
        
        const string FeedUriEnvironmentVariable = "CALAMARI_AUTHFEED";
        const string FeedUsernameEnvironmentVariable = "CALAMARI_AUTHUSERNAME";
        const string FeedPasswordEnvironmentVariable = "CALAMARI_AUTHPASSWORD";

        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");
        static readonly string DownloadPath = TestEnvironment.GetTestPath(TentacleHome, "Files");

        static readonly string PublicFeedUri = "https://www.myget.org/F/octopusdeploy-tests";
        static readonly string NuGetFeedUri = "https://www.nuget.org/api/v2/";
        static readonly string AuthFeedUri = Environment.GetEnvironmentVariable(FeedUriEnvironmentVariable);
        static readonly string FeedUsername = Environment.GetEnvironmentVariable(FeedUsernameEnvironmentVariable);
        static readonly string FeedPassword = Environment.GetEnvironmentVariable(FeedPasswordEnvironmentVariable);
        static readonly string ExpectedPackageHash = "40d78a00090ba7f17920a27cc05d5279bd9a4856";
        static readonly long ExpectedPackageSize = 6346;
        static readonly Feed PublicFeed = new Feed() { Id = "feeds-myget", Version = "1.0.0", PackageId =  "OctoConsole" };
        static readonly Feed FileShare = new Feed() { Id = "feeds-local", Version = "1.0.0", PackageId = "Acme.Web" };
        static readonly Feed AuthFeed = new Feed() { Id = "feeds-authmyget", PackageId =  "OctoConsole", Version = "1.0.0" };
        static readonly Feed NuGetFeed = new Feed() {Id = "feeds-nuget", Version = "2.1.0", PackageId = "abp.castle.log4net" };

        static readonly string ExpectedMavenPackageHash = "3564ef3803de51fb0530a8377ec6100b33b0d073";
        static readonly long ExpectedMavenPackageSize = 2575022;
        static readonly string MavenPublicFeedUri = "https://repo.maven.apache.org/maven2/";
        static readonly Feed MavenPublicFeed = new Feed("#") { Id = "feeds-maven", Version = "22.0", PackageId =  "Maven#com.google.guava#guava" };
        
        static readonly string ExpectedMavenSnapshotPackageHash = "e211b82586ea564b0382cbc87d23854273fc8b2e";
        static readonly long ExpectedMavenSnapshotPackageSize = 2592096;
        static readonly string MavenSnapshotPublicFeedUri = "https://oss.sonatype.org/content/repositories/snapshots/";
        static readonly Feed MavenSnapshotPublicFeed = new Feed("#") { Id = "feeds-maven", Version = "22.0-SNAPSHOT", PackageId =  "Maven#com.google.guava#guava" };
        
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
        public void ShouldDownloadPackage()
        {
            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PublicFeed.PackageId, PublicFeed.Version, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", PublicFeed.DownloadFolder);
#if USE_NUGET_V2_LIBS
            result.AssertOutput("Found package {0} version {1}", PublicFeed.PackageId, PublicFeed.Version);
#endif
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, PublicFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PublicFeed.PackageId, PublicFeed.Version, PublicFeedUri);
        }
        
        [Test]
        public void ShouldDownloadMavenPackage()
        {
            var result = DownloadPackage(
                MavenPublicFeed.PackageId, 
                MavenPublicFeed.Version, 
                MavenPublicFeed.Id, 
                MavenPublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Downloading Maven package {0} {1} from feed: '{2}'", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", 
                MavenPublicFeed.DownloadFolder);
            result.AssertOutput("Found package {0} version {1}", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version);

            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }
        
        [Test]
        public void ShouldDownloadMavenSnapshotPackage()
        {
            var result = DownloadPackage(
                MavenPublicFeed.PackageId, 
                MavenPublicFeed.Version, 
                MavenPublicFeed.Id, 
                MavenPublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Downloading Maven package {0} {1} from feed: '{2}'", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", 
                MavenPublicFeed.DownloadFolder);
            result.AssertOutput("Found package {0} version {1}", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version);

            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", 
                MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }

        [Test]
        public void ShouldDownloadPackageWithRepositoryMetadata()
        {
            var result = DownloadPackage(NuGetFeed.PackageId, NuGetFeed.Version, NuGetFeed.Id, NuGetFeedUri);

            result.AssertSuccess();

            result.AssertOutput(
                $"Downloading NuGet package {NuGetFeed.PackageId} {NuGetFeed.Version} from feed: '{NuGetFeedUri}'");
            result.AssertOutput($"Downloaded package will be stored in: '{NuGetFeed.DownloadFolder}");

            AssertStagePackageOutputVariableSet(result, NuGetFeed.File);
            result.AssertOutput($"Package {NuGetFeed.PackageId} {NuGetFeed.Version} successfully downloaded from feed: '{NuGetFeedUri}'");
        }

        [Test]
        public void ShouldUsePackageFromCache()
        {           
            DownloadPackage(AuthFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri).AssertSuccess();

            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} {1}", PublicFeed.PackageId, PublicFeed.Version);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}", PublicFeed.File);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, PublicFeed.File);
        }
        
        [Test]
        public void ShouldUseMavenPackageFromCache()
        {
            DownloadPackage(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.Id, MavenPublicFeedUri).AssertSuccess();

            var result = DownloadPackage(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.Id, MavenPublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} {1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}", MavenPublicFeed.File);
            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed.File);
        }
        
        [Test]
        public void ShouldUseMavenSnapshotPackageFromCache()
        {
            DownloadPackage(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.Id, MavenPublicFeedUri).AssertSuccess();

            var result = DownloadPackage(MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeed.Id, MavenPublicFeedUri);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} {1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}", MavenPublicFeed.File);
            AssertPackageHashMatchesExpected(result, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(result, MavenPublicFeed.File);
        }


        [Test]
        public void ShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(AuthFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri).AssertSuccess();

            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri, forcePackageDownload: true);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PublicFeed.PackageId, PublicFeed.Version, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", PublicFeed.DownloadFolder);
#if USE_NUGET_V2_LIBS
            result.AssertOutput("Found package {0} version {1}", PublicFeed.PackageId, PublicFeed.Version);
#endif
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, PublicFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PublicFeed.PackageId, PublicFeed.Version, PublicFeedUri);
        }
        
        [Test]        
        public void ShouldByPassCacheAndDownloadMavenPackage()
        {

            var firstDownload = DownloadPackage(
                MavenPublicFeed.PackageId,
                MavenPublicFeed.Version, 
                MavenPublicFeed.Id,
                MavenPublicFeedUri);
            
            firstDownload.AssertSuccess();
            
            var secondDownload = DownloadPackage(
                MavenPublicFeed.PackageId, 
                MavenPublicFeed.Version, 
                MavenPublicFeed.Id,
                MavenPublicFeedUri, 
                forcePackageDownload: true);
            
            secondDownload.AssertSuccess();
            
            secondDownload.AssertOutput("Downloading Maven package {0} {1} from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
            secondDownload.AssertOutput("Downloaded package will be stored in: '{0}'", MavenPublicFeed.DownloadFolder);
            secondDownload.AssertOutput("Found package {0} version {1}", MavenPublicFeed.PackageId, MavenPublicFeed.Version);
            AssertPackageHashMatchesExpected(secondDownload, ExpectedMavenPackageHash);
            AssertPackageSizeMatchesExpected(secondDownload, ExpectedMavenPackageSize);
            AssertStagePackageOutputVariableSet(secondDownload, MavenPublicFeed.File);
            secondDownload.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", MavenPublicFeed.PackageId, MavenPublicFeed.Version, MavenPublicFeedUri);
        }
        
        [Test]        
        public void ShouldByPassCacheAndDownloadMavenSnapshotPackage()
        {

            var firstDownload = DownloadPackage(
                MavenSnapshotPublicFeed.PackageId,
                MavenSnapshotPublicFeed.Version, 
                MavenSnapshotPublicFeed.Id,
                MavenSnapshotPublicFeedUri);
            
            firstDownload.AssertSuccess();
            
            var secondDownload = DownloadPackage(
                MavenSnapshotPublicFeed.PackageId, 
                MavenSnapshotPublicFeed.Version, 
                MavenSnapshotPublicFeed.Id,
                MavenSnapshotPublicFeedUri, 
                forcePackageDownload: true);
            
            secondDownload.AssertSuccess();
            
            secondDownload.AssertOutput("Downloading Maven package {0} {1} from feed: '{2}'", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version, MavenSnapshotPublicFeedUri);
            secondDownload.AssertOutput("Downloaded package will be stored in: '{0}'", MavenSnapshotPublicFeed.DownloadFolder);
            secondDownload.AssertOutput("Found package {0} version {1}", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version);
            AssertPackageHashMatchesExpected(secondDownload, ExpectedMavenSnapshotPackageHash);
            AssertPackageSizeMatchesExpected(secondDownload, ExpectedMavenSnapshotPackageSize);
            AssertStagePackageOutputVariableSet(secondDownload, MavenSnapshotPublicFeed.File);
            secondDownload.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", MavenSnapshotPublicFeed.PackageId, MavenSnapshotPublicFeed.Version, MavenSnapshotPublicFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldDownloadPackage()
        {
            var result = DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", AuthFeed.PackageId, AuthFeed.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", AuthFeed.DownloadFolder);
            result.AssertOutput("Found package {0} version {1}", AuthFeed.PackageId, AuthFeed.Version);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, AuthFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", AuthFeed.PackageId, AuthFeed.Version, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldUsePackageFromCache()
        {
            DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, FeedUsername, FeedPassword)
                .AssertSuccess();

            var result = DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertSuccess();

            result.AssertOutput("Checking package cache for package {0} {1}", AuthFeed.PackageId, AuthFeed.Version);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}", AuthFeed.File);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, AuthFeed.File);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, FeedUsername, FeedPassword).AssertSuccess();

            var result = DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, FeedUsername, FeedPassword, true);

            result.AssertSuccess();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", AuthFeed.PackageId, AuthFeed.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", AuthFeed.DownloadFolder);
            result.AssertOutput("Found package {0} version {1}", AuthFeed.PackageId, AuthFeed.Version);
            AssertPackageHashMatchesExpected(result, ExpectedPackageHash);
            AssertPackageSizeMatchesExpected(result, ExpectedPackageSize);
            AssertStagePackageOutputVariableSet(result, AuthFeed.File);
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", AuthFeed.PackageId, AuthFeed.Version, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldFailDownloadPackageWhenInvalidCredentials()
        {
            var result = DownloadPackage(AuthFeed.PackageId, AuthFeed.Version, AuthFeed.Id, AuthFeedUri, "fake-feed-username", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            result.AssertFailure();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", AuthFeed.PackageId, AuthFeed.Version, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}'", AuthFeed.DownloadFolder);
            result.AssertErrorOutput("Unable to download package: The remote server returned an error: (401) Unauthorized.");
        }

        [Test]
        public void FileShareFeedShouldDownloadPackage()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                var result = DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, acmeWeb.DirectoryPath);

                result.AssertSuccess();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}'", FileShare.DownloadFolder);
                result.AssertOutput("Found package {0} version {1}", FileShare.PackageId, FileShare.Version);
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare.File);
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", FileShare.PackageId, FileShare.Version, acmeWeb.DirectoryPath);
            }
        }
  
        [Test]
        public void FileShareFeedShouldUsePackageFromCache()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, acmeWeb.DirectoryPath).AssertSuccess();

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, acmeWeb.DirectoryPath);
                result.AssertSuccess();

                result.AssertOutput("Checking package cache for package {0} {1}", FileShare.PackageId, FileShare.Version);
                result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}", FileShare.File);
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare.File);
            }
        }

        [Test]
        public void FileShareFeedShouldByPassCacheAndDownloadPackage()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, acmeWeb.DirectoryPath)
                    .AssertSuccess();

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, acmeWeb.DirectoryPath,
                    forcePackageDownload: true);

                result.AssertSuccess();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}'", FileShare.DownloadFolder);
                result.AssertOutput("Found package {0} version {1}", FileShare.PackageId, FileShare.Version);
                AssertPackageHashMatchesExpected(result, acmeWeb.Hash);
                AssertPackageSizeMatchesExpected(result, acmeWeb.Size);
                AssertStagePackageOutputVariableSet(result, FileShare.File);
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", FileShare.PackageId, FileShare.Version, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldFailDownloadPackageWhenInvalidFileShare()
        {
            using (var acmeWeb = CreateSamplePackage())
            {
                var invalidFileShareUri = new Uri(Path.Combine(acmeWeb.DirectoryPath, "InvalidPath"));

                var result = DownloadPackage(FileShare.PackageId, FileShare.Version, FileShare.Id, invalidFileShareUri.ToString());
                result.AssertFailure();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, invalidFileShareUri);
                result.AssertErrorOutput("Failed to download package Acme.Web 1.0.0 from feed: '{0}'", invalidFileShareUri);
                result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'", FileShare.PackageId, FileShare.Version, invalidFileShareUri);
            }
        }

        private TemporaryFile CreateSamplePackage()
        {
            return new TemporaryFile(PackageBuilder.BuildSamplePackage(FileShare.PackageId, FileShare.Version));
        }

     
        [Test]
        public void ShouldFailWhenNoPackageId()
        {
            var result = DownloadPackage("", PublicFeed.Version, PublicFeed.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No package ID was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageId()
        {
            var invalidPackageId = string.Format("X{0}X", PublicFeed.PackageId);
            var result = DownloadPackage(invalidPackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'", invalidPackageId, PublicFeed.Version, PublicFeedUri);
        }

        [Test]
        public void ShouldFailWhenNoFeedVersion()
        {
            var result = DownloadPackage(PublicFeed.PackageId, "", PublicFeed.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No package version was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedVersion()
        {
            const string invalidFeedVersion = "1.0.x";
            var result = DownloadPackage(PublicFeed.PackageId, invalidFeedVersion, PublicFeed.Id, PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("Package version '{0}' specified is not a valid version string", invalidFeedVersion);
        }

        [Test]
        public void ShouldFailWhenNoFeedId()
        {
            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, "", PublicFeedUri);
            result.AssertFailure();

            result.AssertErrorOutput("No feed ID was specified");
        }

        [Test]
        public void ShouldFailWhenNoFeedUri()
        {
            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, "");
            result.AssertFailure();

            result.AssertErrorOutput("No feed URI was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedUri()
        {
            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, "www.myget.org/F/octopusdeploy-tests");
            result.AssertFailure();

            result.AssertErrorOutput("URI specified 'www.myget.org/F/octopusdeploy-tests' is not a valid URI");
        }

        [Test]
        [Ignore("for now, runs fine locally...not sure why it's not failing in TC, will investigate")]
        public void ShouldFailWhenUsernameIsSpecifiedButNoPassword()
        {
            var result = DownloadPackage(PublicFeed.PackageId, PublicFeed.Version, PublicFeed.Id, PublicFeedUri, FeedUsername);
            result.AssertFailure();

            result.AssertErrorOutput("A username was specified but no password was provided");
        }

        CalamariResult DownloadPackage(string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername = "",
            string feedPassword = "",
            bool forcePackageDownload = false,
            int attempts = 5,
            int attemptBackoffSeconds = 0)
        {
            var calamari = Calamari()
                .Action("download-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
                .Argument("feedId", feedId)
                .Argument("feedUri", feedUri)
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

        static void AssertStagePackageOutputVariableSet(CalamariResult result, string filePath)
        {
            result.AssertOutputVariable("StagedPackage.FullPathOnRemoteMachine", Does.StartWith(filePath));
        }

        class Feed
        {
            public Feed()
            {
                Delimiter = ".";
            }
            
            public Feed(string delimiter)
            {
                Delimiter = delimiter;
            }

            private string Delimiter { get; set; }
            
            public string Id { get; set; }

            public string PackageId { get; set; }

            public string Version { get; set; }

            public string DownloadFolder { get { return Path.Combine(DownloadPath, Id); } }

            public string File { get { return Path.Combine(DownloadFolder, PackageId + Delimiter + Version); } }

        }
    }
}
