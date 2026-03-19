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

        var resolved = ResolveLockOptions(lockOptions, scriptIsolationOptions.PromoteToExclusiveLockWhenSharedLockUnavailable);

        if (resolved.Warning is not null)
        {
            Log.Warn(resolved.Warning);
        }

        return resolved.Options;
    }

    internal static ResolvedLockOptions ResolveLockOptions(LockOptions lockOptions, bool promoteToExclusiveLock)
    {
        if (lockOptions.IsFullySupported)
        {
            return new ResolvedLockOptions(lockOptions);
        }

        if (lockOptions.IsSupported)
        {
            // Requested Exclusive Lock
            if (!promoteToExclusiveLock)
            {
                // Warn that other scripts might be running concurrently
                return new ResolvedLockOptions(lockOptions, $"Will acquire {lockOptions.Type} lock, but may run concurrently with other scripts requesting a shared lock");
            }

            return new ResolvedLockOptions(lockOptions);
        }

        if (lockOptions.LockFile.Supports(LockType.Exclusive))
        {
            if (promoteToExclusiveLock)
            {
                var promoted = lockOptions with { Type = LockType.Exclusive };
                return new ResolvedLockOptions(promoted, $"Requested {LockType.Shared} lock is unavailable. Will acquire {promoted.Type} lock");
            }

            return ResolvedLockOptions.NoLockWithWarning($"Requested {lockOptions.Type} lock is unavailable. No lock will be acquired. Running without any isolation.");
        }

        return ResolvedLockOptions.NoLockWithWarning("Unable to support any script isolation. Running without any isolation.");
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

    internal record ResolvedLockOptions(
        LockOptions? Options,
        string? Warning = null
    )
    {
        public static ResolvedLockOptions NoLockWithWarning(string warning) => new(null, warning);
    }
}
