using System.Linq;
using Amazon.CDK.AWS.ECS;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Volume = Octopus.Calamari.Contracts.Aws.Ecs.Volume;

namespace Calamari.Aws.Inputs.Ecs;

public static class VolumeMappingExtensions
{
    public static CfnTaskDefinition.VolumeProperty[] ParseVolumes(this Volume[] volumes)
    {
        if (volumes.Length > 0)
        {
            var boundVolumes =  volumes
                                .Where(v => v.Type ==  VolumeType.Bind).Select(v => new CfnTaskDefinition.VolumeProperty()
                                {
                                    Name =  v.Name,
                                   
                                }).ToArray();
            var efsVolumes = volumes
                             .Where(v => v.Type == VolumeType.Efs)
                             .Select(v => new CfnTaskDefinition.VolumeProperty
                             {
                                 Name = v.Name,
                                 EfsVolumeConfiguration = new CfnTaskDefinition.EFSVolumeConfigurationProperty
                                 {
                                     AuthorizationConfig = new CfnTaskDefinition.AuthorizationConfigProperty
                                     {
                                         Iam = v.EfsIamAuthorization.Equals(true.ToString()) ? "ENABLED" : "DISABLED",
                                         AccessPointId = v.AccessPointId
                                     },
                                    FilesystemId = v.FileSystemId!,
                                    RootDirectory = v.RootDirectory,
                                    TransitEncryption = v.EncryptionInTransit.Equals(true.ToString()) ? "ENABLED" :  "DISABLED", 
                                 }
                             })
                             .ToArray();
            return boundVolumes.Concat(efsVolumes).ToArray();
        }

        return null;
    }
}