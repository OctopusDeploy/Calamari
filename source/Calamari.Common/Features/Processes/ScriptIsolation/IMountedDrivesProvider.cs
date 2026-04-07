using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

interface IMountedDrivesProvider
{
    MountedDrives GetMountedDrives();
}
