using System;
using Calamari.Common.Commands;

namespace Calamari.Common.Plumbing
{
    public static class Guard
    {
        public static void NotNullOrWhiteSpace(string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new CommandException(message);
        }

        public static void NotNull(object value, string message)
        {
            if (value == null)
                throw new ArgumentException(message);
        }
    }
}