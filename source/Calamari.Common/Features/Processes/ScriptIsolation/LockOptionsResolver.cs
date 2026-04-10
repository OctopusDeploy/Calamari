using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class LockOptionsResolver(
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

            LogUnableToSupportAnyScriptIsolation(lockOptions);

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
            LogScriptIsolationAlertServiceMessage(lockOptions, typeEnforced: LockType.Exclusive);
            return lockOptions with { Type = LockType.Exclusive };
        }

        LogUnableToSupportAnyScriptIsolation(lockOptions);
        return null;
    }

    void LogUnableToSupportAnyScriptIsolation(LockOptions originalOptions)
    {
        log.Warn("Unable to support any script isolation. Running without any isolation.");
        LogScriptIsolationAlertServiceMessage(originalOptions, typeEnforced: null);
    }

    void LogScriptIsolationAlertServiceMessage(LockOptions originalOptions, LockType? typeEnforced)
    {
        var alertMessage = BuildScriptIsolationAlertServiceMessage(originalOptions, typeEnforced);
        log.WriteServiceMessage(alertMessage);
    }

    static ServiceMessage BuildScriptIsolationAlertServiceMessage(LockOptions originalOptions, LockType? typeEnforced)
    {
        var requestedType = originalOptions.Type.ToString();
        var enforcedType = typeEnforced?.ToString() ?? "None";
        var fallbackUsed = originalOptions.LockFile.Directory.IsFallback.ToString();
        var capabilityInfo = originalOptions.LockFile.Directory.DetectionResults.Select(r => r.ToString()).StringJoin(", ");
        return new ServiceMessage(
                                  ServiceMessageNames.CalamariScriptIsolationAlert.Name,
                                  new Dictionary<string, string>()
                                  {
                                      { ServiceMessageNames.CalamariScriptIsolationAlert.RequestedTypeAttribute, requestedType },
                                      { ServiceMessageNames.CalamariScriptIsolationAlert.EnforcedTypeAttribute, enforcedType },
                                      { ServiceMessageNames.CalamariScriptIsolationAlert.FallbackUsedAttribute, fallbackUsed },
                                      { ServiceMessageNames.CalamariScriptIsolationAlert.CapabilityInfoAttribute, capabilityInfo }
                                  }
                                 );
    }
}
