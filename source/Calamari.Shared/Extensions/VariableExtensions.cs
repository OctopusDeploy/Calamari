using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Util;

namespace Calamari.Extensions
{
    public static class VariableExtensions
    {
        public static string GetPrimaryPackagePath(this IVariables variables, ICalamariFileSystem fileSystem, bool required)
        {
            var path = variables.Get(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);

            if(string.IsNullOrEmpty(path))
                if (required)
                    throw new CommandException($"The `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` was not specified or blank. This is likely a bug in Octopus, please contact Octopus support.");
                else
                    return null;
            
            path = CrossPlatform.ExpandPathEnvironmentVariables(path);
            if (!fileSystem.FileExists(path))
                throw new CommandException("Could not find package file: " + path);

            return path;
        }
    }
}