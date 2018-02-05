using System;

namespace Calamari.Aws.Exceptions
{
    /// <summary>
    /// Represents some kind of failure during the AWS login
    /// </summary>
    public class LoginException : Exception
    {
        public LoginException()
        {
        }

        public LoginException(string message)
            : base(message)
        {
        }

        public LoginException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}