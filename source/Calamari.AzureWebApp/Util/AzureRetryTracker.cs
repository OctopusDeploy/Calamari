﻿using System;
using Calamari.Common.Plumbing.Retry;

namespace Calamari.AzureWebApp.Util
{
    static class AzureRetryTracker
    {
        /// <summary>
        /// For azure operations, try again after 1s then 2s, 4s etc...
        /// </summary>
        static readonly LimitedExponentialRetryInterval RetryIntervalForAzureOperations = new LimitedExponentialRetryInterval(1000, 30000, 2);

        public static RetryTracker GetDefaultRetryTracker()
        {
            return new RetryTracker(maxRetries: 3,
                timeLimit: TimeSpan.MaxValue,
                retryInterval: RetryIntervalForAzureOperations);
        }
    }
}
