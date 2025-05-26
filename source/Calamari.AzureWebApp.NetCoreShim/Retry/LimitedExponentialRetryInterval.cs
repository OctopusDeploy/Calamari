﻿using System;

namespace Calamari.AzureWebApp.NetCoreShim.Retry
{
    /// <summary>
    /// Implements exponential backoff timing for retry trackers
    /// </summary>
    /// <remarks>
    /// e.g. For network operations, try again after 100ms, then double interval up to 5 seconds
    /// new LimitedExponentialRetryInterval(100, 5000, 2);
    /// <br/><br/>
    /// This is copied from Calamari.Common.Retry as we don't want the Calamari.AzureWebApp.NetCoreShim to take a dependency on any other projects 
    /// </remarks>
    public class LimitedExponentialRetryInterval : AzureWebApp.NetCoreShim.Retry.RetryInterval
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