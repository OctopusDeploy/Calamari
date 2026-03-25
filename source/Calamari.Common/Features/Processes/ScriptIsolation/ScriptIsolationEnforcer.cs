using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Polly;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class ScriptIsolationEnforcer(
    RequestedLockOptionsFactory requestedLockOptionsFactory,
    LockOptionsFactory lockOptionsFactory,
    ILog log)
    : IScriptIsolationEnforcer
{
    public ILockHandle Enforce(CommonOptions.ScriptIsolationOptions scriptIsolationOptions)
    {
        var lockOptions = PrepareLockOptions(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = BuildLockAcquisitionPipeline(lockOptions);
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

    public async Task<ILockHandle> EnforceAsync(
        CommonOptions.ScriptIsolationOptions scriptIsolationOptions,
        CancellationToken cancellationToken
    )
    {
        var lockOptions = PrepareLockOptions(scriptIsolationOptions);
        if (lockOptions is null)
        {
            return new NoLock();
        }

        var pipeline = BuildLockAcquisitionPipeline(lockOptions);
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

    static ResiliencePipeline<ILockHandle> BuildLockAcquisitionPipeline(LockOptions lockOptions)
    {
        return new LockAcquisitionResiliencePipelineBuilder()
            .AddLockOptions(lockOptions)
            .Build();
    }

    LockOptions? PrepareLockOptions(CommonOptions.ScriptIsolationOptions scriptIsolationOptions)
    {
        // Validate the options from the command line
        var requestedOptions = requestedLockOptionsFactory.CreateOrNull(scriptIsolationOptions);
        if (requestedOptions is null)
        {
            return null;
        }

        // Create the actual options, adjusting based on underlying system support
        var lockOptions = lockOptionsFactory.Create(requestedOptions);

        if (lockOptions is null)
        {
            return null;
        }

        log.Verbose($"Acquiring script isolation mutex {lockOptions.Name} with {lockOptions.Type} lock");
        return lockOptions;
    }

    class NoLock : ILockHandle
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
