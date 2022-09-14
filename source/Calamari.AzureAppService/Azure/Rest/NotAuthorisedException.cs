using System;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class NotAuthorisedException : Exception
    {
        public NotAuthorisedException(string message) : base(message)
        {
        }
    }
}