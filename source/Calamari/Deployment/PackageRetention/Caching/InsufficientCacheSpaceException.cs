using System;
using System.Runtime.Serialization;
using Calamari.Common.Features.Scripting.Python;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class InsufficientCacheSpaceException : Exception
    {
        public InsufficientCacheSpaceException()
        {
        }

        public InsufficientCacheSpaceException(string message) : base(message)
        {
        }

        public InsufficientCacheSpaceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InsufficientCacheSpaceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}