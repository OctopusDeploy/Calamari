using System;
using System.Diagnostics;
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

        return DetermineActualLockTypeToUseBasedOnSupport(lockOptions);
    }

    internal LockOptions? DetermineActualLockTypeToUseBasedOnSupport(LockOptions lockOptions)
    {
        var requestedLockType = lockOptions.Type;
        var isExclusiveLockSupported = lockOptions.LockFile.Supports(LockType.Exclusive);
        var isSharedLockSupported = lockOptions.LockFile.Supports(LockType.Shared);

        if (requestedLockType is LockType.Exclusive)
        {
            if (isExclusiveLockSupported)
            {
                return lockOptions;
            }

            LogUnableToSupportAnyScriptIsolation();

            return null;
        }

        Debug.Assert(requestedLockType is LockType.Shared);

        if (isSharedLockSupported)
        {
            return lockOptions;
        }

        if (isExclusiveLockSupported)
        {
            // We escalate in this case because escalating to an exclusive lock will guarantee other
            // things also get the isolation required, at the expense of slowing things down by
            // running things serially.
            log.Warn($"Requested {LockType.Shared} lock is unavailable. Will acquire {LockType.Exclusive} lock.");
            log.Warn("Script is running with elevated isolation level because the filesystem does not support shared file locks.");
            // TODO: Service Message
            return lockOptions with { Type = LockType.Exclusive };
        }

        LogUnableToSupportAnyScriptIsolation();
        return null;
    }

    void LogUnableToSupportAnyScriptIsolation()
    {
        log.Warn("Unable to support any script isolation. Running without any isolation.");
        // TODO: Service Message
    }
}
