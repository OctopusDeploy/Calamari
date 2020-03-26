using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    /// <summary>
    /// A controlled action failure is one whereby Octopus must abort an action
    /// but does not need to reveal additional stack trace information to
    /// the user, e.g. when running an execute script action and the
    /// script returns a failed exit code.
    /// </summary>
    public class ControlledActionFailedException : Exception
    {
        public ControlledActionFailedException(string message)
            : base(message)
        {
        }

        public ControlledActionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}