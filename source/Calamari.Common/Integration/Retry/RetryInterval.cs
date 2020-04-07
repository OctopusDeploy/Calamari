using System;

namespace Calamari.Integration.Retry
{
    public abstract class RetryInterval
    {
        public abstract TimeSpan GetInterval(int retryCount);
    }
}