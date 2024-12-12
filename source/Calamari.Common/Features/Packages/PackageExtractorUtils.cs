using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;

namespace Calamari.Common.Features.Packages
{
    public static class PackageExtractorUtils
    {
        public static void EnsureTargetDirectoryExists(string directory) => Directory.CreateDirectory(directory);

        public static ResiliencePipeline CreateIoExceptionRetryStrategy(ILog log)
        {
            return new ResiliencePipelineBuilder()
                           .AddRetry(new RetryStrategyOptions
                           {
                               ShouldHandle = new PredicateBuilder().Handle<IOException>(),
                               MaxRetryAttempts = 10,
                               Delay = TimeSpan.FromMilliseconds(50),
                               BackoffType = DelayBackoffType.Constant,
                               OnRetry = args =>
                                         {
                                             if (args.Outcome.Exception != null)
                                             {
                                                 log.Verbose($"Failed to extract: {args.Outcome.Exception.Message}. Retry in {args.RetryDelay.Milliseconds} milliseconds.");
                                             }

                                             return default;
                                         }
                           })
                           .Build();
        }
    }
}