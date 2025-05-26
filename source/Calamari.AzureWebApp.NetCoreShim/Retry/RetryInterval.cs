using System;

namespace Calamari.AzureWebApp.NetCoreShim.Retry
{
    /// <remarks>
    /// This is copied from Calamari.Common.Retry as we don't want the Calamari.AzureWebApp.NetCoreShim to take a dependency on any other projects 
    /// </remarks>
    public abstract class RetryInterval
    {
        public abstract TimeSpan GetInterval(int retryCount);
    }
}