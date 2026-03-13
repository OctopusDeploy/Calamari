using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Polly;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public static class Isolation
{
    // Compare these values with the standard script isolation mutex strategy
    static readonly TimeSpan RetryInitialDelay = TimeSpan.FromMilliseconds(10);
    static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMilliseconds(500);

    public static ILockHandle Enforce(CommonOptions.ScriptIsolationOptions scriptIsolationOptions)
    {
        var lockOptions = LockOptions.FromScriptIsolationOptionsOrNull(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = BuildLockAcquisitionPipeline(lockOptions);
        LogIsolation(lockOptions);
        try
        {
            return pipeline.Execute(FileLock.Acquire, lockOptions);
        }
        catch (Exception exception)
        {
            LockRejectedException.Throw(exception);
            throw; // Satisfy the compiler
        }
    }

    public static async Task<ILockHandle> EnforceAsync(
        CommonOptions.ScriptIsolationOptions scriptIsolationOptions,
        CancellationToken cancellationToken
    )
    {
        var lockOptions = LockOptions.FromScriptIsolationOptionsOrNull(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = BuildLockAcquisitionPipeline(lockOptions);
        LogIsolation(lockOptions);
        try
        {
            return await pipeline.ExecuteAsync(static (o, _) => ValueTask.FromResult(FileLock.Acquire(o)), lockOptions, cancellationToken);
        }
        catch (Exception exception)
        {
            LockRejectedException.Throw(exception);
            throw; // Satisfy the compiler
        }
    }

    static void LogIsolation(LockOptions lockOptions)
    {
        Log.Verbose($"Acquiring script isolation mutex {lockOptions.Name} with {lockOptions.Type} lock");
    }

    static ResiliencePipeline<ILockHandle> BuildLockAcquisitionPipeline(LockOptions lockOptions)
    {
        var builder = new ResiliencePipelineBuilder<ILockHandle>();
        // Timeout must be between 10ms and 1 day. (Polly)
        // If it's 10ms or less, we'll skip timeout and limit retries
        // If it's more than 1 day, we'll assume indefinite retries with no timeout
        var retryAttempts = lockOptions.Timeout <= TimeSpan.FromMilliseconds(10)
            ? 1
            : int.MaxValue;
        if (lockOptions.Timeout < TimeSpan.FromDays(1) && lockOptions.Timeout > TimeSpan.FromMilliseconds(10))
        {
            builder.AddTimeout(lockOptions.Timeout);
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
        return builder.Build();
    }

    class NoLock : ILockHandle
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
