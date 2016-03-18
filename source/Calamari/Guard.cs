using System;
using Calamari.Commands.Support;

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
