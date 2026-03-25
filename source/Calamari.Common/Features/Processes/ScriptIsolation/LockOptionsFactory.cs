using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class LockOptionsFactory(
    ILockDirectoryFactory lockDirectoryFactory,
    ILog log)
{
    public LockOptions? Create(RequestedLockOptions requestedOptions)
    {
        var lockDirectory = lockDirectoryFactory.Create(requestedOptions.PreferredLockDirectory);

        var lockFile = lockDirectory.GetLockFile($"ScriptIsolation.{requestedOptions.MutexName}.lock");

        var lockOptions = new LockOptions(requestedOptions.Type, requestedOptions.MutexName, lockFile, requestedOptions.Timeout);

        return UseExclusiveIfSharedIsNotSupported(lockOptions);
    }

    internal LockOptions? UseExclusiveIfSharedIsNotSupported(LockOptions lockOptions)
    {
        if (lockOptions.BothSharedAndExclusiveAreSupported)
        {
            return lockOptions;
        }

        if (lockOptions.RequestedLockTypeIsSupported)
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
