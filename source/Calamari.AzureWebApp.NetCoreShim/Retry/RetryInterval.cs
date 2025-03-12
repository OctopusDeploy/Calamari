using System;

namespace Calamari.AzureWebApp.NetCoreShim.Retry
{
    public abstract class RetryInterval
    {
        public abstract TimeSpan GetInterval(int retryCount);
    }
}