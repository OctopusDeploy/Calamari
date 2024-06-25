using System;
using System.Net.Http;
using Polly;
using Polly.Retry;

namespace Calamari.Testing;

public static class TestingRetryPolicies
{
    public static ResiliencePipeline CreateGoogleCloudHttpRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
               .AddRetry(new RetryStrategyOptions
               {
                   ShouldHandle = args => args.Outcome switch
                                          {
                                              { Exception: HttpRequestException } => PredicateResult.True(),
                                              { Result: HttpResponseMessage { IsSuccessStatusCode: false } } => PredicateResult.True(),
                                              _ => PredicateResult.False()
                                          },
                   BackoffType = DelayBackoffType.Exponential,
                   UseJitter = true,
                   MaxRetryAttempts = 5,
                   Delay = TimeSpan.FromSeconds(5)
               })
               .Build();
    }
}