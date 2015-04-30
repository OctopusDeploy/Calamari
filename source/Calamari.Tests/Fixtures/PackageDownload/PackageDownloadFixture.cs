using System;
using System.Globalization;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
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
        const string StagedPackageHashVariableName = "StagedPackage.Hash";
        const string StagedPackageSizeVariableName = "StagedPackage.Size";
        const string StagedPackageFullPathOnRemoteMachine = "StagedPackage.FullPathOnRemoteMachine";

        string PublicFeedId = "feeds-myget";
        string PublicFeedUri = "https://www.myget.org/F/octopusdeploy-tests";
        string AuthFeedId = "feeds-authmyget";
        string AuthFeedUri = Environment.GetEnvironmentVariable(FeedUriEnvironmentVariable);
        string FeedUsername = Environment.GetEnvironmentVariable(FeedUsernameEnvironmentVariable);
        string FeedPassword = Environment.GetEnvironmentVariable(FeedPasswordEnvironmentVariable);
        string InvalidFeedUsername = "totally-fake-user";
        string InvalidFeedPassword = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        string FileShareFeedId = "feeds-local";

        string PackageId = "OctoConsole";
        string InvalidPackageId = "OctConsole";
        string PackageVersion = "1.0.0.0";
        string InvalidPackageVersion = "1.0.0.x";
        string ExpectedPackageHash = "40d78a00090ba7f17920a27cc05d5279bd9a4856";
        string ExpectedPackageSize = "6346";
        string FileSharePackageId = "Acme.Web";
        string FileSharePackageVersion = "1.0.0";

        static readonly string PackageDownloadFolder = GetPackageDownloadFolder("PackageDownload");

        CalamariResult DownloadPackage(string packageId,
            string packageVersion,
            string feedId,
            string feedUri,
            string feedUsername = "",
            string feedPassword = "",
            bool forcePackageDownload = false)
        {
            var calamari = Calamari()
                .Action("download-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
                .Argument("feedId", feedId)
                .Argument("feedUri", feedUri);

            if (!String.IsNullOrWhiteSpace(feedUsername))
                calamari.Argument("feedUsername", feedUsername);

            if (!String.IsNullOrWhiteSpace(feedPassword))
                calamari.Argument("feedPassword", feedPassword);

            if (forcePackageDownload)
                calamari.Flag("forcePackageDownload");

            return Invoke(calamari);

        }

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(PackageDownloadFolder))
                Directory.CreateDirectory(PackageDownloadFolder);

            Directory.SetCurrentDirectory(PackageDownloadFolder);
            Environment.SetEnvironmentVariable("TentacleHome", PackageDownloadFolder);
        }

        [TearDown]
        public void TearDown()
        {
            var workingDirectory = Path.Combine(PackageDownloadFolder, "Files");
            if(Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        public void ShouldDownloadPackage()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'", PackageDownloadFolder, PublicFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", PublicFeedId) +
                    Path.DirectorySeparatorChar + 
                    PackageId + 
                    "." +
                    PackageVersion));
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        public void ShouldUsePackageFromCache()
        {
            DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri)
                .AssertZero();

            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri);

            result.AssertZero();

            result.AssertOutput("Checking package cache for package {0} {1}", PackageId,
                PackageVersion);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Files\\{1}\\{2}.{3}",
                    PackageDownloadFolder, PublicFeedId, PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", PublicFeedId) +
                    Path.DirectorySeparatorChar +
                    PackageId +
                    "." +
                    PackageVersion));
        }

        [Test]
        public void ShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri)
                .AssertZero();

            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri, forcePackageDownload: true);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'", PackageDownloadFolder, PublicFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", PublicFeedId) +
                    Path.DirectorySeparatorChar +
                    PackageId +
                    "." +
                    PackageVersion));
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldDownloadPackage()
        {
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'",
               PackageDownloadFolder, AuthFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", AuthFeedId) +
                    Path.DirectorySeparatorChar +
                    PackageId +
                    "." +
                    PackageVersion));
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldUsePackageFromCache()
        {
            DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword)
                .AssertZero();

            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword);

            result.AssertZero();

            result.AssertOutput("Checking package cache for package {0} {1}", PackageId,
                PackageVersion);
            result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Files\\{1}\\{2}.{3}",
                    PackageDownloadFolder, AuthFeedId, PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", AuthFeedId) +
                    Path.DirectorySeparatorChar +
                    PackageId +
                    "." +
                    PackageVersion));
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldByPassCacheAndDownloadPackage()
        {
            DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword)
                .AssertZero();

            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, FeedUsername, FeedPassword, true);

            result.AssertZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'",
                PackageDownloadFolder, AuthFeedId);
            result.AssertOutput("Found package {0} version {1}", PackageId, PackageVersion);
            result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(ExpectedPackageHash));
            result.AssertOutputVariable(StagedPackageSizeVariableName, Is.EqualTo(ExpectedPackageSize));
            result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                Is.StringStarting(
                    Path.Combine(PackageDownloadFolder, "Files", AuthFeedId) +
                    Path.DirectorySeparatorChar +
                    PackageId +
                    "." +
                    PackageVersion));
            result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", PackageId, PackageVersion, AuthFeedUri);
        }

        [Test]
        [AuthenticatedTest(FeedUriEnvironmentVariable, FeedUsernameEnvironmentVariable, FeedPasswordEnvironmentVariable)]
        public void PrivateNuGetFeedShouldFailDownloadPackageWhenInvalidCredentials()
        {
            var result = DownloadPackage(PackageId, PackageVersion, AuthFeedId, AuthFeedUri, InvalidFeedUsername, InvalidFeedPassword);

            result.AssertNonZero();

            result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", PackageId,
                PackageVersion, AuthFeedUri);
            result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'", PackageDownloadFolder, AuthFeedId);
            result.AssertErrorOutput("Unable to download package: The remote server returned an error: (401) Unauthorized.");
        }

        [Test]
        public void FileShareFeedShouldDownloadPackage()
        {
            using (var acmeWeb = CreateSampleBackage())
            {
                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);

                result.AssertZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'", PackageDownloadFolder, FileShareFeedId);
                result.AssertOutput("Found package {0} version {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(acmeWeb.Hash));
                result.AssertOutputVariable(StagedPackageSizeVariableName,
                    Is.EqualTo(acmeWeb.Size.ToString(CultureInfo.InvariantCulture)));
                result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                    Is.StringStarting(
                        Path.Combine(PackageDownloadFolder, "Files", FileShareFeedId) +
                        Path.DirectorySeparatorChar +
                        FileSharePackageId +
                        "." +
                        FileSharePackageVersion));
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldUsePackageFromCache()
        {
            using (var acmeWeb = CreateSampleBackage())
            {
                DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath)
                    .AssertZero();

                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath);
                result.AssertZero();

                result.AssertOutput("Checking package cache for package {0} {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutput("Package was found in cache. No need to download. Using file: '{0}\\Files\\{1}\\{2}.{3}",
                       PackageDownloadFolder, FileShareFeedId, FileSharePackageId, FileSharePackageVersion);
                result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(acmeWeb.Hash));
                result.AssertOutputVariable(StagedPackageSizeVariableName,
                    Is.EqualTo(acmeWeb.Size.ToString(CultureInfo.InvariantCulture)));
                result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                    Is.StringStarting(
                        Path.Combine(PackageDownloadFolder, "Files", FileShareFeedId) +
                        Path.DirectorySeparatorChar +
                        FileSharePackageId +
                        "." +
                        FileSharePackageVersion));
            }
        }

        [Test]
        public void FileShareFeedShouldByPassCacheAndDownloadPackage()
        {
            using (var acmeWeb = CreateSampleBackage())
            {
                DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath)
                    .AssertZero();

                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, acmeWeb.DirectoryPath,
                    forcePackageDownload: true);

                result.AssertZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId, 
                    FileSharePackageVersion, new Uri(acmeWeb.DirectoryPath));
                result.AssertOutput("Downloaded package will be stored in: '{0}\\Files\\{1}'", PackageDownloadFolder, FileShareFeedId);
                result.AssertOutput("Found package {0} version {1}", FileSharePackageId, FileSharePackageVersion);
                result.AssertOutputVariable(StagedPackageHashVariableName, Is.EqualTo(acmeWeb.Hash));
                result.AssertOutputVariable(StagedPackageSizeVariableName,
                    Is.EqualTo(acmeWeb.Size.ToString(CultureInfo.InvariantCulture)));
                result.AssertOutputVariable(StagedPackageFullPathOnRemoteMachine,
                    Is.StringStarting(
                        Path.Combine(PackageDownloadFolder, "Files", FileShareFeedId) +
                        Path.DirectorySeparatorChar +
                        FileSharePackageId +
                        "." +
                        FileSharePackageVersion));
                result.AssertOutput("Package {0} {1} successfully downloaded from feed: '{2}'", 
                    FileSharePackageId, FileSharePackageVersion, acmeWeb.DirectoryPath);
            }
        }

        [Test]
        public void FileShareFeedShouldFailDownloadPackageWhenInvalidFileShare()
        {
            using (var acmeWeb = CreateSampleBackage())
            {
                var invalidFileShareUri = Path.Combine(acmeWeb.DirectoryPath, "InvalidPath");

                var result = DownloadPackage(FileSharePackageId, FileSharePackageVersion, FileShareFeedId, invalidFileShareUri);
                result.AssertNonZero();

                result.AssertOutput("Downloading NuGet package {0} {1} from feed: '{2}'", FileSharePackageId,
                    FileSharePackageVersion, new Uri(invalidFileShareUri));
                result.AssertErrorOutput("Unable to download package: Could not find package {0} {1} in feed: '{2}'",
                    FileSharePackageId, FileSharePackageVersion, new Uri(invalidFileShareUri));
                result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'",
                    FileSharePackageId, FileSharePackageVersion, invalidFileShareUri);
            }
        }

        private TemporaryFile CreateSampleBackage()
        {
            return new TemporaryFile(PackageBuilder.BuildSamplePackage(FileSharePackageId, FileSharePackageVersion));
        }

        [Test]
        [Ignore("Need to get this setup and running somehow...need to think of a way to do it so it works across borders (aka TC or other members of the team)")]
        public void FileShareFeedShouldFailDownloadPackageWhenNoPermissions()
        {
            //TODO: Yeah
        }

        [Test]
        public void ShouldFailWhenNoPackageId()
        {
            var result = DownloadPackage("", PackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No package ID was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageId()
        {
            var result = DownloadPackage(InvalidPackageId, PackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("Failed to download package {0} {1} from feed: '{2}'", InvalidPackageId, PackageVersion, PublicFeedUri);
        }

        [Test]
        public void ShouldFailWhenNoPackageVersion()
        {
            var result = DownloadPackage(PackageId, "", PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No package version was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageVersion()
        {
            var result = DownloadPackage(PackageId, InvalidPackageVersion, PublicFeedId, PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("Package version '{0}' specified is not a valid semantic version", InvalidPackageVersion);
        }

        [Test]
        public void ShouldFailWhenNoFeedId()
        {
            var result = DownloadPackage(PackageId, PackageVersion, "", PublicFeedUri);
            result.AssertNonZero();

            result.AssertErrorOutput("No feed ID was specified");
        }

        [Test]
        public void ShouldFailWhenNoFeedUri()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, "");
            result.AssertNonZero();

            result.AssertErrorOutput("No feed URI was specified");
        }

        [Test]
        public void ShouldFailWhenInvalidFeedUri()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, "www.myget.org/F/octopusdeploy-tests");
            result.AssertNonZero();

            result.AssertErrorOutput("URI specified 'www.myget.org/F/octopusdeploy-tests' is not a valid URI");
        }

        [Test]
        [Ignore("for now, runs fine locally...not sure why it's not failing in TC, will investigate")]
        public void ShouldFailWhenUsernameIsSpecifiedButNoPassword()
        {
            var result = DownloadPackage(PackageId, PackageVersion, PublicFeedId, PublicFeedUri, FeedUsername);
            result.AssertNonZero();

            result.AssertErrorOutput("A username was specified but no password was provided");
        }
    }
}
