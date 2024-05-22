using System;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class KubernetesFileSystem : CalamariPhysicalFileSystem
    {
        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            if (Environment.GetEnvironmentVariable(KubernetesEnvironmentVariables.KubernetesAgentVolumeFreeBytes) is
                { } freeSpaceString)
            {
                return ulong.TryParse(freeSpaceString, out totalNumberOfFreeBytes);
            }

            totalNumberOfFreeBytes = 0;
            return false;
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes)
        {
            if (Environment.GetEnvironmentVariable(KubernetesEnvironmentVariables.KubernetesAgentVolumeTotalBytes) is
                { } totalSpaceString)
            {
                return ulong.TryParse(totalSpaceString, out totalNumberOfBytes);
            }

            totalNumberOfBytes = 0;
            return false;
        }
    }
}