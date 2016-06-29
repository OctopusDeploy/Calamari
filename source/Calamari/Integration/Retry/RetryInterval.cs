using System;

namespace Calamari.Integration.Retry
{
    /// <summary>
    /// Implements exponential backoff timing for retry trackers
    /// </summary>
    /// <remarks>
    /// e.g. For network operations, try again after 100ms, then double interval up to 5 seconds
    /// new RetryInterval(100, 5000, 2);
    ///</remarks>
    public class RetryInterval
    {
        readonly TimeSpan retryInterval;
        readonly TimeSpan maxInterval;

        public RetryInterval(TimeSpan retryInterval, TimeSpan maxInterval)
        {
            this.retryInterval = retryInterval;
            this.maxInterval = maxInterval;
        }

        public TimeSpan GetInterval(int retryCount)
        {
            var delayTime = TimeSpan.FromMilliseconds(retryInterval.Milliseconds*Math.Pow(2, retryCount));
            return delayTime > maxInterval 
                ? maxInterval 
                : delayTime;
        }
    }
}