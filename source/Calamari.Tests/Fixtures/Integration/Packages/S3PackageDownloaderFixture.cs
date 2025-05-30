﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Requirements;
using Calamari.Tests.AWS;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning;
using TestEnvironment = Calamari.Testing.Helpers.TestEnvironment;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class S3PackageDownloaderFixture : CalamariFixture
    {
        string rootDir;
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");
        readonly string region;
        readonly string bucketName;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        
        public S3PackageDownloaderFixture()
        {
            region = RegionRandomiser.GetARegion();
            bucketName = $"calamari-e2e-{Guid.NewGuid():N}";
        }

        [OneTimeSetUp]
        public async Task TestFixtureSetUp()
        {
            rootDir = GetFixtureResource(this.GetType().Name);
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, true);
            }
            
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
            await Validate(async client => await client.PutBucketAsync(bucketName));
            Directory.CreateDirectory(GetFixtureResource(rootDir));
        }

        [OneTimeTearDown]
        public async Task TestFixtureTearDown()
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, true);
            }
            
            Environment.SetEnvironmentVariable("TentacleHome", null);
            await Validate(async client =>
                     {
                         var response = await client.ListObjectsAsync(bucketName);
                         foreach (var s3Object in response.S3Objects)
                         {
                             await client.DeleteObjectAsync(bucketName, s3Object.Key);
                         }
                         await client.DeleteBucketAsync(bucketName);
                     });
        }
        
        static S3PackageDownloader GetDownloader()
        {
            return new S3PackageDownloader(new CalamariVariables(), ConsoleLog.Instance, CalamariPhysicalFileSystem.GetPhysicalFileSystem());
        }

        [Test]
        public async Task CanDownloadPackage()
        {
            string filename = "Acme.Core.1.0.0.0-bugfix.zip";
            string packageId = $"{bucketName}/Acme.Core";
            var version = VersionFactory.CreateVersion("1.0.0.0-bugfix", VersionFormat.Semver);
            
            File.Copy(GetFixtureResource("Samples", filename), Path.Combine(rootDir, filename));

            await Validate(async client => await client.PutObjectAsync(new PutObjectRequest
                                                                       {
                                                                           BucketName = bucketName,
                                                                           Key = filename,
                                                                           InputStream = File.OpenRead(Path.Combine(rootDir, filename))
                                                                       },
                                                                       cancellationToken));

            var downloader = GetDownloader();
            var package = downloader.DownloadPackage(packageId,
                                                     version,
                                                     "s3-feed",
                                                     new Uri("https://please-ignore.com"),
                                                     await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken),
                                                     await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken),
                                                     true,
                                                     3,
                                                     TimeSpan.FromSeconds(3));

            package.PackageId.Should().Be(packageId);
            package.Size.Should().Be(new FileInfo(Path.Combine(rootDir, filename)).Length);
        }

        protected async Task Validate(Func<AmazonS3Client, Task> execute)
        {
            var credentials = new BasicAWSCredentials(
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken),
                                                      await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken));

            var config = new AmazonS3Config {AllowAutoRedirect = true, RegionEndpoint = RegionEndpoint.GetBySystemName(region)};
            
            using (var client = new AmazonS3Client(credentials, config))
            {
                await execute(client);
            }
        }
    }
}
