using System;

namespace Calamari.Common.Plumbing.Retry
{
    /// <summary>
    /// Implements exponential backoff timing for retry trackers
    /// </summary>
    /// <remarks>
    /// e.g. For network operations, try again after 100ms, then double interval up to 5 seconds
    /// new LimitedExponentialRetryInterval(100, 5000, 2);
    /// </remarks>
    public class LimitedExponentialRetryInterval : RetryInterval
    {
        readonly int retryInterval;
        readonly int maxInterval;
        readonly double multiplier;

        public LimitedExponentialRetryInterval(int retryInterval, int maxInterval, double multiplier)
        {
            this.retryInterval = retryInterval;
            this.maxInterval = maxInterval;
            this.multiplier = multiplier;
        }

        public override TimeSpan GetInterval(int retryCount)
        {
            var delayTime = retryInterval * Math.Pow(multiplier, retryCount);
            if (delayTime > maxInterval)
                return TimeSpan.FromMilliseconds(maxInterval);
            return TimeSpan.FromMilliseconds((int)delayTime);
        }
    }
}