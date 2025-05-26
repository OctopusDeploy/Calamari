using System;

namespace Calamari.AzureWebApp.NetCoreShim.Retry
{
    /// <summary>
    /// Implements a linear backoff interval as retryCount * retryInterval
    /// </summary>
    /// <remarks>
    /// This is copied from Calamari.Common.Retry as we don't want the Calamari.AzureWebApp.NetCoreShim to take a dependency on any other projects 
    /// </remarks>
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