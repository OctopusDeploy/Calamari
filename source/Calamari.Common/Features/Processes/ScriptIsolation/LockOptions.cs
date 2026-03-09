using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    FileInfo LockFile,
    TimeSpan Timeout
)
{
    public static LockOptions? FromScriptIsolationOptionsOrNull(CommonOptions.ScriptIsolationOptions options)
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
            timeout = TimeSpan.MaxValue;
        }
        else if (!TimeSpan.TryParse(options.Timeout, out timeout))
        {
            Log.Verbose($"Failed to parse mutex timeout value '{options.Timeout}' as TimeSpan. Defaulting to TimeSpan.MaxValue.");
            timeout = TimeSpan.MaxValue;
        }

        var lockFileInfo = GetLockFileInfo(options.TentacleHome, options.MutexName);
        return new LockOptions(lockType.Value, options.MutexName, lockFileInfo, timeout);
    }

    static void LogIfPartiallyConfigured(CommonOptions.ScriptIsolationOptions options)
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
        Log.Verbose($"Some script isolation options were provided, but the following required {optionIsOrAre} missing: {string.Join(", ", missingOptions)}");
        LogIsolationWillNotBeEnforced();
    }

    static void LogIsolationWillNotBeEnforced()
    {
        Log.Verbose("Script isolation will not be enforced.");
    }

    static FileInfo GetLockFileInfo(string tentacleHome, string mutexName) =>
        new(Path.Combine(tentacleHome, $"ScriptIsolation.{mutexName}.lock"));

    static LockType? MapScriptIsolationLevelToLockTypeOrNull(string isolationLevel) =>
        isolationLevel.ToLowerInvariant() switch
        {
            "fullisolation" => LockType.Exclusive,
            "noisolation" => LockType.Shared,
            _ => null
        };
}