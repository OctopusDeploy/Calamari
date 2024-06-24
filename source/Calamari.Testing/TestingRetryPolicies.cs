using System;
using System.Net.Http;
using Polly;
using Polly.Retry;

namespace Calamari.Testing;

public static class TestingRetryPolicies
{
    public static ResiliencePipeline CreateHttpRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
               .AddRetry(new RetryStrategyOptions
               {
                   ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(),
                   BackoffType = DelayBackoffType.Exponential,
                   UseJitter = true,
                   MaxRetryAttempts = 5,
                   Delay = TimeSpan.FromSeconds(5)
               })
               .Build();
    }
}