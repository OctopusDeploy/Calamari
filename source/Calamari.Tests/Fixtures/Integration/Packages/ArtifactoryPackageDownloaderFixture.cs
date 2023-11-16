using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class ArtifactoryPackageDownloaderFixture
    {
        CalamariPhysicalFileSystem fileSystem;
        string testDirectory;
        string currentDirectory;
        string cacheDirectory;

        public ArtifactoryPackageDownloaderFixture()
        {
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        }
        
        [OneTimeSetUp] 
        public void Setup()
        {
            testDirectory = "TestFileCache";
            currentDirectory = Directory.GetCurrentDirectory();
            cacheDirectory = Path.Combine(currentDirectory, testDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
        }

        [OneTimeTearDown] 
        public void TearDown()
        {
            fileSystem.DeleteDirectory(cacheDirectory);
        }
        //
        // [Test]
        // public void AttemptsOnlyOnceIfSuccessful()
        // {
        //     var packageId = "generic-local/TestWebApp";
        //     var version = VersionFactory.CreateSemanticVersion(1, 0, 2);
        //     var feedUri = new Uri("https://octopusdeploytest.jfrog.io");
        //     var feedUsername = "ben.pearce@octopus.com";
        //     var feedPassword = "A password";
        //     var feedCredentials = new NetworkCredential(feedUsername, feedPassword);
        //
        //     var log = Substitute.For<ILog>();
        //
        //     var downloader = new ArtifactoryPackageDownloader(log, fileSystem);
        //
        //     var packagePhysicalFile = downloader.DownloadPackage(packageId,
        //                                version,
        //                                feedUri,
        //                                feedCredentials,
        //                                cacheDirectory);
        //
        //     packagePhysicalFile.PackageId.Should().Be("generic-local/TestWebApp");
        //     packagePhysicalFile.Version.ToString().Should().Be("1.0.2");
        //     packagePhysicalFile.Size.Should().Be(679677);
        //     packagePhysicalFile.Extension.Should().Be(".zip");
        //     packagePhysicalFile.Hash.Should().Be("d6230604262fa191c6ace5d047562084ae863fbf");
        //     
        // }
    }
}