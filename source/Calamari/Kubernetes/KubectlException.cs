using System;
using Calamari.Common.Commands;

namespace Calamari.Kubernetes.Integration
{
    public class KubectlException : CommandException
    {
        public KubectlException(string message) : base(message)
        {
        }

        public KubectlException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}