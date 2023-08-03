using System;
using System.Net;
using System.Net.Http;
using Polly;
using Polly.Retry;

namespace Calamari.AzureAppService
{
    public static class RetryPolicies
    {
        static readonly Random Jitterer = new Random();

        // Based on the logic in the Polly.Extensions.Http package, but without having to include the package
        // We add a small amount of random jitter to just offset the retries
        public static RetryPolicy<HttpResponseMessage> TransientHttpErrorsPolicy { get; } = Policy.Handle<HttpRequestException>()
                                                                                                  .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                                                                                                  .WaitAndRetryAsync(5,
                                                                                                                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Jitterer.Next(0, 1000)));
    }
}