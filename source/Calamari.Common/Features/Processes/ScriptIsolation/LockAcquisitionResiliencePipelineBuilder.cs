using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

class LockAcquisitionResiliencePipelineBuilder(ResiliencePipelineBuilder<ILockHandle> builder)
{
    static readonly TimeSpan RetryInitialDelay = TimeSpan.FromMilliseconds(10);
    static readonly TimeSpan RetryMaxDelay = TimeSpan.FromMilliseconds(500);

    public LockAcquisitionResiliencePipelineBuilder()
        : this(new ResiliencePipelineBuilder<ILockHandle>())
    {
    }

    public LockAcquisitionResiliencePipelineBuilder AddLockOptions(LockOptions options)
    {
        // If it's 10ms or less, we'll skip timeout and limit retries
        var retryAttempts = options.Timeout <= TimeSpan.FromMilliseconds(10) && options.Timeout != Timeout.InfiniteTimeSpan
            ? 1
            : int.MaxValue;
        if (options.Timeout > TimeSpan.FromMilliseconds(10))
        {
            builder.AddTimeout(
                               new TimeoutStrategyOptions
                               {
                                   // Using a timeout generator does not constrain the timeout to
                                   // a maximum of 1 day
                                   TimeoutGenerator = _ => ValueTask.FromResult(options.Timeout)
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
        return this;
    }

    public ResiliencePipeline<ILockHandle> Build() => builder.Build();
}
