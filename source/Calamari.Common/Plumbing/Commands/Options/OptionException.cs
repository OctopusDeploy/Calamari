using System;

namespace Calamari.Common.Plumbing.Commands.Options;

public class OptionException : Exception
{
    public OptionException(string message)
        : base(message)
    {
    }

    public OptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
