using System;
using System.Diagnostics.CodeAnalysis;
using Calamari.Common.Commands;

namespace Calamari.Common.Plumbing
{
    public static class Guard
    {
#if HAS_NULLABLE_REF_TYPES
        public static void NotNullOrWhiteSpace([NotNull]string? value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new CommandException(message);
        }

        public static void NotNull([NotNull]object? value, string message)
        {
            if (value == null)
                throw new ArgumentException(message);
        }
#else
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
#endif
    }
}