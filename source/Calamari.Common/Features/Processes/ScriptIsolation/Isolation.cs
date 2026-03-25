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
        var requestedOptions = new RequestedLockOptionsFactory(ConsoleLog.Instance).CreateOrNull(scriptIsolationOptions);
        if (requestedOptions is null)
        {
            return null;
        }

        var pathResolutionService = DefaultPathResolutionService.Instance;
        var mountedDrives = new SystemMountedDrivesProvider(pathResolutionService).GetMountedDrives();
        var fileLockService = FileLockService.Instance;
        var lockDirectoryFactory = new LockDirectoryFactory(mountedDrives, fileLockService);
        var lockOptions = new LockOptionsFactory(lockDirectoryFactory, ConsoleLog.Instance).Create(requestedOptions);

        if (lockOptions is null)
        {
            return null;
        }

        LogIsolation(lockOptions);
        return lockOptions;
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
