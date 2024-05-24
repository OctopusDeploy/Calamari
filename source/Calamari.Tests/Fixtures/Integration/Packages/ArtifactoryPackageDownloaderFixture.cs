using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
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
                Uri feedUri;
                NetworkCredential feedCredentials;
                ArtifactoryPackageDownloader downloader;
                static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
                readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        
                public ArtifactoryPackageDownloaderFixture()
                {
                    fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                }
        
                [OneTimeSetUp] 
                public async Task Setup()
                {
                    testDirectory = "TestFileCache";
                    currentDirectory = Directory.GetCurrentDirectory();
                    cacheDirectory = Path.Combine(currentDirectory, testDirectory);
                    fileSystem.EnsureDirectoryExists(cacheDirectory);

                    feedUri = new Uri("https://octopusdeploy.jfrog.io");
                    var sensitiveValue = await ExternalVariables.Get(ExternalVariable.ArtifactoryE2EPassword, cancellationToken);
                    feedCredentials = new NetworkCredential("", sensitiveValue.ToString());
        
                    var log = Substitute.For<ILog>();
                    var variables = new CalamariVariables
                    {
                        { "ArtifactoryGenericFeed.Regex", @"(?<orgPath>.+?)/(?<module>[^/]+)/(?<module>\2)-(?<baseRev>[^/]+?)\.(?<ext>(?:(?!\d))[^\-/]+|7z)" },
                        { "ArtifactoryGenericFeed.Repository", "generic-repo-server-tests" }
                    };

                    downloader = new ArtifactoryPackageDownloader(log, fileSystem, variables);
                }
        
                [OneTimeTearDown] 
                public void TearDown()
                {
                    fileSystem.DeleteDirectory(cacheDirectory);
                }
                
        [Test]
        public void Downloads_TestWebApp_Zip()
        {
            var packageId = "com/octopus/TestWebApp2/TestWebApp2";
            var version = VersionFactory.CreateSemanticVersion(1, 0, 0);

            var packagePhysicalFile = downloader.DownloadPackage(packageId,
                                                                 version,
                                                                 feedUri,
                                                                 feedCredentials,
                                                                 cacheDirectory, 
                                                                 5, 
                                                                 TimeSpan.FromSeconds(1));

            packagePhysicalFile.PackageId.Should().Be("com/octopus/TestWebApp2/TestWebApp2");
            packagePhysicalFile.Version.ToString().Should().Be("1.0.0");
            packagePhysicalFile.Size.Should().Be(679677);
            packagePhysicalFile.Extension.Should().Be(".zip");
            packagePhysicalFile.Hash.Should().Be("d6230604262fa191c6ace5d047562084ae863fbf");
        }

        [Test]
        public void Downloads_OctopusClient_Nupkg()
        {
            var packageId = "octopus/Octopus.Client/Octopus.Client";
            var version = VersionFactory.CreateSemanticVersion(13, 0, 4037);

            var packagePhysicalFile = downloader.DownloadPackage(packageId,
                                                                 version,
                                                                 feedUri,
                                                                 feedCredentials,
                                                                 cacheDirectory, 
                                                                 5, 
                                                                 TimeSpan.FromSeconds(1));

            // The packageId for nupkgs uses the id from the nuspec file.
            packagePhysicalFile.PackageId.Should().Be("Octopus.Client");
            packagePhysicalFile.Version.ToString().Should().Be("13.0.4037");
            packagePhysicalFile.Size.Should().Be(1531810);
            packagePhysicalFile.Extension.Should().Be(".nupkg");
            packagePhysicalFile.Hash.Should().Be("57f07be223748b17e081b56a2dba043fdf3f7e22");
        }

        [Test]
        public void Downloads_AcmeWeb_TarGz()
        {
            var packageId = "octopus/Acme.Web/Acme.Web";
            var version = VersionFactory.CreateSemanticVersion(3, 2, 5);

            var packagePhysicalFile = downloader.DownloadPackage(packageId,
                                                                 version,
                                                                 feedUri,
                                                                 feedCredentials,
                                                                 cacheDirectory, 
                                                                 5, 
                                                                 TimeSpan.FromSeconds(1));

            packagePhysicalFile.PackageId.Should().Be("octopus/Acme.Web/Acme.Web");
            packagePhysicalFile.Version.ToString().Should().Be("3.2.5");
            packagePhysicalFile.Size.Should().Be(387121);
            packagePhysicalFile.Extension.Should().Be(".tar.gz");
            packagePhysicalFile.Hash.Should().Be("0b58492e7eef21639e77b0f2b5137bbe3c377c6e");
        }
    }
}