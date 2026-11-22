using System;
using Calamari.Integration.Packages.Download;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class S3PackageDownloaderConfigFixture
    {
        [Test]
        public void CreateS3Config_NoEndpoint_UsesRegionEndpoint()
        {
            var config = S3PackageDownloader.CreateS3Config("ap-southeast-2", null);

            config.RegionEndpoint.SystemName.Should().Be("ap-southeast-2");
            config.ServiceURL.Should().BeNull();
            config.ForcePathStyle.Should().BeFalse();
            config.AllowAutoRedirect.Should().BeTrue();
        }

        [Test]
        public void CreateS3Config_WithEndpoint_UsesServiceUrlPathStyleAndSigningRegion()
        {
            var config = S3PackageDownloader.CreateS3Config("us-east-1", "http://localhost:9000");

            config.ServiceURL.Should().StartWith("http://localhost:9000");
            config.ForcePathStyle.Should().BeTrue();
            config.AuthenticationRegion.Should().Be("us-east-1");
        }
    }
}
