using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Timeout;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    FileInfo LockFile,
    TimeSpan Timeout
)
{
    static readonly TimeSpan RetryInitialDelay = TimeSpan.FromMilliseconds(10);
    static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMilliseconds(500);

    public ResiliencePipeline<ILockHandle> BuildLockAcquisitionPipeline()
    {
        var builder = new ResiliencePipelineBuilder<ILockHandle>();
        return AddLockOptions(builder).Build();
    }

    public ResiliencePipelineBuilder<ILockHandle> AddLockOptions(ResiliencePipelineBuilder<ILockHandle> builder)
    {
        // If it's 10ms or less, we'll skip timeout and limit retries
        var retryAttempts = Timeout <= TimeSpan.FromMilliseconds(10) && Timeout != System.Threading.Timeout.InfiniteTimeSpan
            ? 1
            : int.MaxValue;
        if (Timeout > TimeSpan.FromMilliseconds(10))
        {
            builder.AddTimeout(
                               new TimeoutStrategyOptions
                               {
                                   // Using a timeout generator does not constrain the timeout to
                                   // a maximum of 1 day
                                   TimeoutGenerator = _ => ValueTask.FromResult(Timeout)
                               }
                              );
        }

        builder.AddRetry(
                         new()
                         {
                             BackoffType = DelayBackoffType.Exponential,
                             Delay = RetryInitialDelay,
                             MaxDelay = RetryMaxDelay,
                             MaxRetryAttempts = retryAttempts,
                             ShouldHandle = new PredicateBuilder<ILockHandle>().Handle<LockRejectedException>(),
                             UseJitter = true
                         }
                        );
        return builder;
    }

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
            timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }
        else if (!TimeSpan.TryParse(options.Timeout, out timeout))
        {
            Log.Verbose($"Failed to parse mutex timeout value '{options.Timeout}' as TimeSpan. Defaulting to Infinite.");
            timeout = System.Threading.Timeout.InfiniteTimeSpan;
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

    static FileInfo GetLockFileInfo(string tentacleHome, string mutexName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            if (mutexName.Contains(invalidChar))
            {
                throw new ArgumentException($"Invalid mutex name '{mutexName}'.");
            }
        }

        return new FileInfo(Path.Combine(tentacleHome, $"ScriptIsolation.{mutexName}.lock"));
    }

    static LockType? MapScriptIsolationLevelToLockTypeOrNull(string isolationLevel) =>
        isolationLevel.ToLowerInvariant() switch
        {
            "fullisolation" => LockType.Exclusive,
            "noisolation" => LockType.Shared,
            _ => null
        };
}