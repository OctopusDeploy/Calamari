using System;
using System.Runtime.Serialization;

namespace Calamari.Aws.Deployment
{
    public class AmazonFileUploadException : Exception
    {
        public AmazonFileUploadException(){}
        public AmazonFileUploadException(string message) : base(message){}
        public AmazonFileUploadException(string message, Exception innerException) : base(message, innerException){}
    }
}