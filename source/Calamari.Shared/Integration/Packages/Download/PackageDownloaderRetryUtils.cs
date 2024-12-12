using System;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;

namespace Calamari.Integration.Packages.Download
{
    public static class PackageDownloaderRetryUtils
    {
        public static ResiliencePipeline CreateRetryStrategy<T>(int maxAttempts, TimeSpan failureBackoff, ILog log) where T : Exception
        {
            return new ResiliencePipelineBuilder()
                   .AddRetry(new RetryStrategyOptions
                   {
                       ShouldHandle = new PredicateBuilder().Handle<T>(),
                       MaxRetryAttempts = maxAttempts,
                       Delay = failureBackoff,
                       BackoffType = DelayBackoffType.Linear,
                       OnRetry = args =>
                                 {
                                     log.Verbose($"Waiting {args.RetryDelay.TotalSeconds}s before attempting the download from the external feed again.");
                                     return default;
                                 }
                   })
                   .Build();
        }
    }
}