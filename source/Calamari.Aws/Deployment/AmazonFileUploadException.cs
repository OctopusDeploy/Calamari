using System;

namespace Calamari.Aws.Deployment
{
    public class AmazonFileUploadException : Exception
    {
        public AmazonFileUploadException(string message) : base(message){}
        public AmazonFileUploadException(string message, Exception innerException) : base(message, innerException){}
    }
}