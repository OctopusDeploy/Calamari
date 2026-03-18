using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public static class Isolation
{
    public static ILockHandle Enforce(CommonOptions.ScriptIsolationOptions scriptIsolationOptions)
    {
        var lockOptions = PrepareLockOptions(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = lockOptions.BuildLockAcquisitionPipeline();
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
        var lockOptions = PrepareLockOptions(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = lockOptions.BuildLockAcquisitionPipeline();
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

    static LockOptions? PrepareLockOptions(CommonOptions.ScriptIsolationOptions scriptIsolationOptions)
    {
        var lockOptions = LockOptions.FromScriptIsolationOptionsOrNull(scriptIsolationOptions);

        if (lockOptions is null)
        {
            return null;
        }

        LogIsolation(lockOptions);

        return ResolveLockOptions(lockOptions, scriptIsolationOptions.PromoteToExclusiveLockWhenSharedLockUnavailable);
    }

    internal static LockOptions? ResolveLockOptions(LockOptions lockOptions, bool promoteToExclusiveLock)
    {
        if (lockOptions.IsFullySupported)
        {
            return lockOptions;
        }

        if (lockOptions.IsSupported)
        {
            // Requested Exclusive Lock
            if (!promoteToExclusiveLock)
            {
                // Warn that other scripts might be running concurrently
                Log.Warn($"Will acquire {lockOptions.Type} lock, but may run concurrently with other scripts requesting a shared lock");
            }

            return lockOptions;
        }

        if (lockOptions.LockFile.Supports(LockType.Exclusive))
        {
            if (promoteToExclusiveLock)
            {
                lockOptions = lockOptions with
                {
                    Type = LockType.Exclusive
                };
                Log.Warn($"Requested {LockType.Shared} lock is unavailable. Will acquire {lockOptions.Type} lock");
                return lockOptions;
            }

            Log.Warn($"Requested {lockOptions.Type} lock is unavailable. No lock will be acquired. Running without any isolation.");
            return null;
        }

        Log.Warn("Unable to support any script isolation. Running without any isolation.");

        return null;
    }

    static void LogIsolation(LockOptions lockOptions)
    {
        Log.Verbose($"Acquiring script isolation mutex {lockOptions.Name} with {lockOptions.Type} lock");
    }

    class NoLock : ILockHandle
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
