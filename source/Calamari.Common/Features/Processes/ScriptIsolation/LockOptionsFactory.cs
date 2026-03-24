using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class LockOptionsFactory(ILog log)
{
    public LockOptions? Create(RequestedLockOptions requestedOptions)
    {
        var lockDirectory = LockDirectory.GetLockDirectory(requestedOptions.PreferredLockDirectory.FullName);

        var lockFile = lockDirectory.GetLockFile($"ScriptIsolation.{requestedOptions.MutexName}.lock");

        var lockOptions = new LockOptions(requestedOptions.Type, requestedOptions.MutexName, lockFile, requestedOptions.Timeout);

        if (lockOptions.IsFullySupported)
        {
            return lockOptions;
        }

        if (lockOptions.IsSupported)
        {
            // Requested Exclusive lock
            return lockOptions;
        }

        if (lockOptions.LockFile.Supports(LockType.Exclusive))
        {
            log.Warn($"Requested {LockType.Shared} lock is unavailable. Will acquire {LockType.Exclusive} lock.");
            return lockOptions with { Type = LockType.Exclusive };
        }

        log.Warn("Unable to support any script isolation. Running without any isolation.");
        return null;
    }
}
