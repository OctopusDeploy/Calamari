using System;

namespace Calamari.Common.Plumbing.Retry
{
    public abstract class RetryInterval
    {
        public abstract TimeSpan GetInterval(int retryCount);
    }
}