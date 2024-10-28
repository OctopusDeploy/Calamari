using System;

namespace Calamari.AzureWebApp.NetCoreShim.Retry
{
    /// <summary>
    /// Implements a linear backoff interval as retryCount * retryInterval
    /// </summary>
    public class LinearRetryInterval : AzureWebApp.NetCoreShim.Retry.RetryInterval
    {
        readonly TimeSpan retryInterval;

        public LinearRetryInterval(TimeSpan retryInterval)
        {
            this.retryInterval = retryInterval;
        }

        public override TimeSpan GetInterval(int retryCount)
        {
            return TimeSpan.FromMilliseconds((int)retryInterval.TotalMilliseconds * retryCount);
        }
    }
}