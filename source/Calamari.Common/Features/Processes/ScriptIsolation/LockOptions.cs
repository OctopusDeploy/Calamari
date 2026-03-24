using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Timeout;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed record LockOptions(
    LockType Type,
    string Name,
    LockFile LockFile,
    TimeSpan Timeout
)
{
    static readonly TimeSpan RetryInitialDelay = TimeSpan.FromMilliseconds(10);
    static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Indicates whether file locking is supported for the configured location. This requires
    /// that both Exclusive and Shared locks are supported on the underlying filesystem.
    /// </summary>
    public bool IsFullySupported => LockFile.IsFullySupported;

    /// <summary>
    /// Indicates whether the specific type of lock is supported on the underlying file system.
    /// </summary>
    public bool IsSupported => LockFile.Supports(Type);

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
        var requestedOptions = new RequestedLockOptionsFactory(ConsoleLog.Instance).CreateOrNull(options);
        if (requestedOptions is null)
        {
            return null;
        }

        var lockOptions = new LockOptionsFactory(ConsoleLog.Instance).Create(requestedOptions);
        return lockOptions;
    }
}
