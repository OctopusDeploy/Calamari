using Calamari.Common.Features.Processes;

namespace Calamari.Testing;

public static class CommandLineTestArgumentsExtensionMethods
{
    public static CommandLine VariablesFileArguments(this CommandLine commandLine, string filePath, string encryptionKey)
    {
        return commandLine.Argument("variables", filePath)
                          .Argument("variablesPassword", encryptionKey);
    }
}