using System.IO;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public interface ILockDirectoryFactory
{
    LockDirectory Create(DirectoryInfo preferredLockDirectory);
}
