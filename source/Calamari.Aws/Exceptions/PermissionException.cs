using System;

namespace Calamari.Aws.Exceptions
{
    /// <summary>
    /// Represents a failed attempt to query the AWS API
    /// </summary>
    public class PermissionException : Exception
    {
        public PermissionException()
        {
        }

        public PermissionException(string message)
            : base(message)
        {
        }

        public PermissionException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}