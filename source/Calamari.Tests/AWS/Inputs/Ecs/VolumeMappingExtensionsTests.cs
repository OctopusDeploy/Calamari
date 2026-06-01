using System;
using Calamari.Aws.Inputs.Ecs;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;
using InputVolume = Octopus.Calamari.Contracts.Aws.Ecs.Volume;

namespace Calamari.Tests.AWS.Inputs.Ecs;

[TestFixture]
public class VolumeMappingExtensionsTests
{
    [Test]
    public void ParseVolumes_WhenEmpty_ReturnsNull()
    {
        var result = Array.Empty<InputVolume>().ParseVolumes();

        result.Should().BeNull();
    }

    [Test]
    public void ParseVolumes_WithBindVolume_MapsNameOnly()
    {
        var volumes = new[]
        {
            new InputVolume { Type = VolumeType.Bind, Name = "scratch" }
        };

        var result = volumes.ParseVolumes();

        result.Should().HaveCount(1);
        result![0].Name.Should().Be("scratch");
        result[0].EFSVolumeConfiguration.Should().BeNull();
    }

    [Test]
    public void ParseVolumes_WithEfsVolume_MapsFullEfsConfiguration()
    {
        var volumes = new[]
        {
            new InputVolume
            {
                Type = VolumeType.Efs,
                Name = "shared-data",
                FileSystemId = "fs-0123abcd",
                AccessPointId = "fsap-0123abcd",
                RootDirectory = "/data",
                EncryptionInTransit = "True",
                EfsIamAuthorization = "True"
            }
        };

        var result = volumes.ParseVolumes();

        result.Should().HaveCount(1);
        result![0].Name.Should().Be("shared-data");

        var efs = result[0].EFSVolumeConfiguration;
        efs.Should().NotBeNull();
        efs!.FilesystemId.Should().Be("fs-0123abcd");
        efs.RootDirectory.Should().Be("/data");
        efs.TransitEncryption.Should().Be("ENABLED");

        efs.AuthorizationConfig.Should().NotBeNull();
        efs.AuthorizationConfig!.Iam.Should().Be("ENABLED");
        efs.AuthorizationConfig.AccessPointId.Should().Be("fsap-0123abcd");
    }

    [Test]
    public void ParseVolumes_EfsVolume_DefaultsTransitEncryptionAndIamToDisabled()
    {
        var volumes = new[]
        {
            new InputVolume
            {
                Type = VolumeType.Efs,
                Name = "shared-data",
                FileSystemId = "fs-0123abcd",
                EncryptionInTransit = string.Empty,
                EfsIamAuthorization = string.Empty
            }
        };

        var result = volumes.ParseVolumes();

        var efs = result![0].EFSVolumeConfiguration;
        efs!.TransitEncryption.Should().Be("DISABLED");
        efs.AuthorizationConfig!.Iam.Should().Be("DISABLED");
    }

    [Test]
    public void ParseVolumes_EfsVolume_TransitEncryptionAndIamAreCaseSensitive()
    {
        // The implementation compares to true.ToString() == "True" — lowercase "true" should not enable.
        var volumes = new[]
        {
            new InputVolume
            {
                Type = VolumeType.Efs,
                Name = "shared-data",
                FileSystemId = "fs-0123abcd",
                EncryptionInTransit = "true",
                EfsIamAuthorization = "true"
            }
        };

        var result = volumes.ParseVolumes();

        var efs = result![0].EFSVolumeConfiguration;
        efs!.TransitEncryption.Should().Be("DISABLED");
        efs.AuthorizationConfig!.Iam.Should().Be("DISABLED");
    }

    [Test]
    public void ParseVolumes_OrdersBindVolumesBeforeEfsVolumes()
    {
        var volumes = new[]
        {
            new InputVolume { Type = VolumeType.Efs, Name = "efs-1", FileSystemId = "fs-1", EncryptionInTransit = "True", EfsIamAuthorization = "True" },
            new InputVolume { Type = VolumeType.Bind, Name = "bind-1" },
            new InputVolume { Type = VolumeType.Efs, Name = "efs-2", FileSystemId = "fs-2", EncryptionInTransit = "True", EfsIamAuthorization = "True" },
            new InputVolume { Type = VolumeType.Bind, Name = "bind-2" }
        };

        var result = volumes.ParseVolumes();

        result.Should().HaveCount(4);
        result![0].Name.Should().Be("bind-1");
        result[1].Name.Should().Be("bind-2");
        result[2].Name.Should().Be("efs-1");
        result[3].Name.Should().Be("efs-2");
    }
}
