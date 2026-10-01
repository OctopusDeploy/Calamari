using System;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

interface ICachedDriveInfoProvider
{
    CachedDriveInfo GetAssociatedDrive(string path);
}
