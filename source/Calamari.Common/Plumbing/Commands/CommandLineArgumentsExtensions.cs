using System;
using Calamari.Common.Plumbing.Commands.Options;

namespace Calamari.Common.Plumbing.Commands
{
    public static class CommandLineArgumentsExtensions
    {
        public static void ParseArgument(this string[] commandLineArguments, string argumentName, Action<string> action)
        {
            var customOptions = new OptionSet();
            customOptions.Add($"{argumentName}=",$"", action);
            customOptions.Parse(commandLineArguments);
        }
    }
}