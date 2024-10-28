using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;

namespace Calamari.Azure.AppServices
{
    public static class RetryPolicies
    {
        public static class ContextKeys
        {
            public const string Log = nameof(Log);
        };

        static readonly Random Jitterer = new Random();

        // Based on the logic in the Polly.Extensions.Http package, but without having to include the package
        // We add a small amount of random jitter to just offset the retries
        public static AsyncRetryPolicy<HttpResponseMessage> TransientHttpErrorsPolicy { get; } = Policy.Handle<HttpRequestException>()
                                                                                                       .Or<SocketException>()
                                                                                                       .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                                                                                                       .WaitAndRetryAsync(6,
                                                                                                                          retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Jitterer.Next(0, 1000)));

        // This is specifically for retries in tests, we retry fewer times with a higher base to try avoid hitting rate limiting in Azure.
        // The Jitter offset has been increased to try stagger requests between parallel test runs.
        public static AsyncRetryPolicy<HttpResponseMessage> TestsTransientHttpErrorsPolicy { get; } = Policy.Handle<HttpRequestException>()
                                                                                                            .Or<SocketException>()
                                                                                                            .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                                                                                                            .WaitAndRetryAsync(4,
                                                                                                                               retryAttempt => TimeSpan.FromSeconds(Math.Pow(3.5, retryAttempt)) + TimeSpan.FromMilliseconds(Jitterer.Next(0, 10000)));

        public static AsyncRetryPolicy<HttpResponseMessage> AsynchronousZipDeploymentOperationPolicy { get; } = Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.Accepted)
                                                                                                                      .WaitAndRetryForeverAsync((_1, ctx) => TimeSpan.FromSeconds(4),
                                                                                                                                                (response, timeout, ctx) =>
                                                                                                                                                {
                                                                                                                                                    if (ctx.TryGetValue(ContextKeys.Log, out var logObj) && logObj is ILog log)
                                                                                                                                                    {
                                                                                                                                                        log.Verbose($"Zip deployment not completed. Received HTTP {(int)response.Result.StatusCode}. Next attempt in {timeout}.");
                                                                                                                                                    }
                                                                                                                                                });
    }
}