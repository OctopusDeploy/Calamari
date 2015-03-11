using System;

namespace Octopus.Deploy.Startup
{
    public class CommandException : Exception
    {
        public CommandException(string message)
            : base(message)
        {
        }
    }
}