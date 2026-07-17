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

        /// <summary>
        /// Throws if the archive entry key would resolve to a path outside the intended extraction root (zip-slip / path traversal).
        /// </summary>
        public static void ThrowIfPathTraversalAttempted(string entryKey, string extractionDirectory)
        {
            var extractionRoot = Path.GetFullPath(extractionDirectory);
            // Ensure the root ends with a separator so "root" can't be a prefix of "rootEvil"
            if (!extractionRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                extractionRoot += Path.DirectorySeparatorChar;

            var destination = Path.GetFullPath(Path.Combine(extractionDirectory, entryKey));

            if (!destination.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Archive entry '{entryKey}' would extract to '{destination}', which is outside the intended extraction directory '{extractionDirectory}'. The archive may be malicious.");
        }

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