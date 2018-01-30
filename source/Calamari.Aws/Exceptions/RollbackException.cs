using System;

namespace Calamari.Aws.Exceptions
{
    /// <summary>
    /// Represents a failed deployment that resulted in a rollback state
    /// </summary>
    public class RollbackException : Exception
    {
        public RollbackException()
        {
        }

        public RollbackException(string message)
            : base(message)
        {
        }

        public RollbackException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}