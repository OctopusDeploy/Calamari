using System;
using Calamari.Commands.Support;
using Calamari.Extensibility;

namespace Calamari
{
    public static class Guard
    {
        public static void NotNullOrWhiteSpace(string value, string message)
        {
            if (String.IsNullOrWhiteSpace(value))
                throw new CommandException(message);
        }
    }
}
