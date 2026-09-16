using System;
using Calamari.Common.Commands;

namespace Calamari.Aws.Exceptions
{
    /// <summary>
    /// Represents an AWS API call rejected because the request rate limit was exceeded
    /// </summary>
    public class RateExceededException : CommandException
    {
        public RateExceededException(string message)
            : base(message)
        {
        }

        public RateExceededException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
