using System;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    string LockFile,
    TimeSpan Timeout
)
{
    public static LockOptions? FromScriptIsolationOptionsOrNull(CommonOptions.ScriptIsolationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Level) || string.IsNullOrWhiteSpace(options.MutexName) || string.IsNullOrWhiteSpace(options.TentacleHome))
        {
            return null;
        }

        var lockType = MapScriptIsolationLevelToLockTypeOrNull(options.Level);
        if (lockType == null)
        {
            Log.Verbose($"Failed to map script isolation level '{options.Level}' to a valid LockType. Expected 'FullIsolation' or 'NoIsolation' (case-insensitive).");
            return null;
        }

        if (!TimeSpan.TryParse(options.Timeout, out var timeout))
        {
            Log.Verbose($"Failed to parse mutex timeout value '{options.Timeout}' as TimeSpan. Defaulting to TimeSpan.MaxValue.");
            // What should we do if the timeout is invalid? Default to max value?
            timeout = TimeSpan.MaxValue;
        }

        var lockFilePath = GetLockFilePath(options.TentacleHome, options.MutexName);
        return new LockOptions(lockType.Value, options.MutexName, lockFilePath, timeout);
    }

    static string GetLockFilePath(string tentacleHome, string mutexName) =>
        System.IO.Path.Combine(tentacleHome, $"ScriptIsolation.{mutexName}.lock"); // Should we sanitize the mutex name or just allow it to be invalid?

    static LockType? MapScriptIsolationLevelToLockTypeOrNull(string isolationLevel) =>
        isolationLevel.ToLowerInvariant() switch
        {
            "fullisolation" => LockType.Exclusive,
            "noisolation" => LockType.Shared,
            _ => null
        };
}
