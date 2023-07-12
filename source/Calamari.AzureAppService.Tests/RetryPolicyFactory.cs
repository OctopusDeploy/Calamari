using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites.Models;
using Polly;
using Polly.Retry;

namespace Calamari.AzureAppService.Tests
{
    public static class RetryPolicyFactory
    {
        public static RetryPolicy CreateForHttp429()
        {
            return Policy
                .Handle<DefaultErrorResponseException>(ex => (int)ex.Response.StatusCode == 429)
                .WaitAndRetryAsync(5,
                                   CalculateRetryDelay,
                                   (ex, ts, i, ctx) => Task.CompletedTask);
        }
        
        static TimeSpan CalculateRetryDelay(int retryAttempt, Exception ex, Context ctx)
        {
            //the azure API returns a Retry-After header that contains the number of seconds because you should try again
            //so we try and read that header
            if (ex is DefaultErrorResponseException responseException && responseException.Response.Headers.TryGetValue("Retry-After", out var values))
            {
                var retryAfterValue = values.FirstOrDefault();

                if (int.TryParse(retryAfterValue, out var secondsToRetryAfter))
                    return TimeSpan.FromSeconds(secondsToRetryAfter);
            }

            //fall back on just an exponential increase  based on the retry attempt
            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        }
    }
}