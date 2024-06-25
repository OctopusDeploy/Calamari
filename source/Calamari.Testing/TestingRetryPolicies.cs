using System;
using System.Net.Http;
using Polly;

namespace Calamari.Testing;

public static class TestingRetryPolicies
{
    static readonly Random Jitterer = new();
    
    public static IAsyncPolicy<HttpResponseMessage> CreateGoogleCloudHttpRetryPipeline()
    {
        return Policy.Handle<HttpRequestException>()
                     .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                     .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Jitterer.Next(0, 1000)));
    }
}