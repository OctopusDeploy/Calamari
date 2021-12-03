using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.FileSystem
{
    public class FreeSpaceChecker : IFreeSpaceChecker
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;

        readonly string skipFreeDiskSpaceCheckVariable = "OctopusSkipFreeDiskSpaceCheck";
        readonly string freeDiskSpaceOverrideInMegaBytesVariable = "OctopusFreeDiskSpaceOverrideInMegaBytes";

        public FreeSpaceChecker(ICalamariFileSystem fileSystem, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
        }

        public ulong GetRequiredSpace(string directoryPath)
        {
            ulong requiredSpaceInBytes = 500L * 1024 * 1024;
            var freeSpaceOverrideInMegaBytes = variables.GetInt32(freeDiskSpaceOverrideInMegaBytesVariable);

            if (freeSpaceOverrideInMegaBytes.HasValue)
            {
                requiredSpaceInBytes = (ulong) freeSpaceOverrideInMegaBytes * 1024 * 1024;
                Log.Verbose($"{freeDiskSpaceOverrideInMegaBytesVariable} has been specified. We will check and ensure that the drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' has {((ulong) requiredSpaceInBytes).ToFileSizeString()} free disk space.");
            }
            
            var success = fileSystem.GetDiskFreeSpace(directoryPath, out ulong totalNumberOfFreeBytes);
            if (!success)
                return 0;

            if (totalNumberOfFreeBytes < requiredSpaceInBytes)
                return requiredSpaceInBytes - totalNumberOfFreeBytes;

            return 0;
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            if (CalamariEnvironment.IsRunningOnMono && CalamariEnvironment.IsRunningOnMac)
            {
                //After upgrading to macOS 10.15.2, and mono 5.14.0, drive.TotalFreeSpace and drive.AvailableFreeSpace both started returning 0.
                //see https://github.com/mono/mono/issues/17151, which was fixed in mono 6.4.xx
                //If we upgrade mono past 5.14.x, scriptcs stops working.
                //Rock and a hard place.
                Log.Verbose("Unable to determine disk free space under Mono on macOS. Assuming there's enough.");
                return;
            }

            if (variables.GetFlag(skipFreeDiskSpaceCheckVariable))
            {
                Log.Verbose($"{skipFreeDiskSpaceCheckVariable} is enabled. The check to ensure that the drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' has enough free space will be skipped.");
                return;
            }

            ulong requiredSpaceInBytes = 500L * 1024 * 1024;
            var freeSpaceOverrideInMegaBytes = variables.GetInt32(freeDiskSpaceOverrideInMegaBytesVariable);

            if (freeSpaceOverrideInMegaBytes.HasValue)
            {
                requiredSpaceInBytes = (ulong)freeSpaceOverrideInMegaBytes * 1024 * 1024;
                Log.Verbose($"{freeDiskSpaceOverrideInMegaBytesVariable} has been specified. We will check and ensure that the drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' has {requiredSpaceInBytes.ToFileSizeString()} free disk space.");
            }

            var success = fileSystem.GetDiskFreeSpace(directoryPath, out var totalNumberOfFreeBytes);
            if (!success)
                return;

            if (totalNumberOfFreeBytes < requiredSpaceInBytes)
                throw new CommandException($"The drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' does not have enough free disk space available for this operation to proceed. The disk only has {totalNumberOfFreeBytes.ToFileSizeString()} available; please free up at least {requiredSpaceInBytes.ToFileSizeString()}.");
        }
    }
}