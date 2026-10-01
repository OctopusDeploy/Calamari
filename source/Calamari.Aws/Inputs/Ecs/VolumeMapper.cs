using System.Linq;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Cfn = Calamari.Aws.Integration.Ecs.Deploy.Cfn;
using InputVolume = Octopus.Calamari.Contracts.Aws.Ecs.Volume;

namespace Calamari.Aws.Inputs.Ecs;

public static class VolumeMappingExtensions
{
    // SPF always emits Volumes as an array — empty becomes [] not omitted.
    public static Cfn.Volume[] ParseVolumes(this InputVolume[] volumes)
    {
        if (volumes.Length == 0) return [];

        var boundVolumes = volumes.Where(v => v.Type == VolumeType.Bind)
                                  .Select(v => new Cfn.Volume { Name = v.Name });

        var efsVolumes = volumes.Where(v => v.Type == VolumeType.Efs)
                                .Select(v => new Cfn.Volume
                                {
                                    Name = v.Name,
                                    EFSVolumeConfiguration = new Cfn.EfsVolumeConfiguration
                                    {
                                        FilesystemId      = v.FileSystemId!,
                                        RootDirectory     = v.RootDirectory,
                                        TransitEncryption = v.EncryptionInTransit.Equals(true.ToString()) ? "ENABLED" : "DISABLED",
                                        AuthorizationConfig = new Cfn.AuthorizationConfig
                                        {
                                            Iam = v.EfsIamAuthorization.Equals(true.ToString()) ? "ENABLED" : "DISABLED",
                                            // SPF didn't appear to be outputting this; we add it because it seems correct.
                                            // No customer has complained, so we may not have anyone leveraging EFS IAM Auth.
                                            AccessPointId = v.AccessPointId
                                        }
                                    }
                                });

        return boundVolumes.Concat(efsVolumes).ToArray();
    }
}
