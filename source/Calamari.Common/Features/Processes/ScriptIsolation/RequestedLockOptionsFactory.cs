using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class RequestedLockOptionsFactory(
    ILog log
)
{
    public RequestedLockOptions? CreateFromIsolationOptions(CommonOptions.ScriptIsolationOptions options)
    {
        if (!options.FullyConfigured)
        {
            LogIfPartiallyConfigured(options);
            return null;
        }

        var lockType = MapScriptIsolationLevelToLockTypeOrNull(options.Level);
        if (lockType == null)
        {
            Log.Verbose($"Failed to map script isolation level '{options.Level}' to a valid LockType. Expected 'FullIsolation' or 'NoIsolation' (case-insensitive).");
            LogIsolationWillNotBeEnforced();
            return null;
        }

        TimeSpan timeout;

        if (string.IsNullOrWhiteSpace(options.Timeout))
        {
            timeout = Timeout.InfiniteTimeSpan;
        }
        else if (!TimeSpan.TryParse(options.Timeout, out timeout))
        {
            Log.Verbose($"Failed to parse mutex timeout value '{options.Timeout}' as TimeSpan. Defaulting to Infinite.");
            timeout = Timeout.InfiniteTimeSpan;
        }

        var preferredLockDirectory = new DirectoryInfo(options.TentacleHome);
        ValidateMutexName(options.MutexName);
        return new RequestedLockOptions(lockType.Value, options.MutexName, timeout,  preferredLockDirectory);
    }

    void LogIfPartiallyConfigured(CommonOptions.ScriptIsolationOptions options)
    {
        if (!options.PartiallyConfigured)
        {
            return;
        }

        var missingOptions = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Level))
        {
            missingOptions.Add("scriptIsolationLevel");
        }

        if (string.IsNullOrWhiteSpace(options.MutexName))
        {
            missingOptions.Add("scriptIsolationMutexName");
        }

        if (string.IsNullOrWhiteSpace(options.TentacleHome))
        {
            missingOptions.Add("TentacleHome (Environment Variable)");
        }

        var optionIsOrAre = missingOptions.Count > 1 ? "options are" : "option is";
        log.Verbose($"Some script isolation options were provided, but the following required {optionIsOrAre} missing: {string.Join(", ", missingOptions)}");
        LogIsolationWillNotBeEnforced();
    }

    void LogIsolationWillNotBeEnforced()
    {
        log.Verbose("ScriptIsolation will not be enforced");
    }

    static LockType? MapScriptIsolationLevelToLockTypeOrNull(string isolationLevel) =>
        isolationLevel.ToLowerInvariant() switch
        {
            "fullisolation" => LockType.Exclusive,
            "noisolation" => LockType.Shared,
            _ => null
        };

    static void ValidateMutexName(string mutexName)
    {
        // Mutex must only contain characters that are valid in a file name
        foreach (var invalidChange in Path.GetInvalidFileNameChars())
        {
            if (mutexName.Contains(invalidChange))
            {
                throw new ArgumentException($"Invalid mutex name '{mutexName}'.");
            }
        }
    }
}
