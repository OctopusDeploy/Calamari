using System;

namespace Calamari.Common.Plumbing.Retry
{
    /// <summary>
    /// Implements a linear backoff interval as retryCount * retryInterval
    /// </summary>
    public class LinearRetryInterval : RetryInterval
    {
        private readonly TimeSpan retryInterval;

        public LinearRetryInterval(TimeSpan retryInterval)
        {
            this.retryInterval = retryInterval;
        }

        public override TimeSpan GetInterval(int retryCount)
        {
            return TimeSpan.FromMilliseconds((int) retryInterval.TotalMilliseconds * retryCount);
        }
    }
}