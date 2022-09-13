using System;

namespace Calamari.Aws.Exceptions
{
    /// <summary>
    /// Represents an unknown exception that will be passed back to the user
    /// </summary>
    public class UnknownException : Exception
    {
        public UnknownException()
        {
        }

        public UnknownException(string message)
            : base(message)
        {
        }

        public UnknownException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}