using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

interface ILockDirectoryFactory
{
    LockDirectory Create(DirectoryInfo preferredLockDirectory);
}
