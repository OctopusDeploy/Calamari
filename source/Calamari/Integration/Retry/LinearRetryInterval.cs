using System;

namespace Calamari.Integration.Retry
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

        public override int GetInterval(int retryCount)
        {
            return (int)retryInterval.TotalMilliseconds * retryCount;
        }
    }
}