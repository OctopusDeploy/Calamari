using Calamari.Commands.Support;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Util;

namespace Calamari.Extensions
{
    public static class VariableExtensions
    {
        public static PathToPackage GetPathToPrimaryPackage(this IVariables variables, ICalamariFileSystem fileSystem, bool required)
        {
            var path = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);

            if(string.IsNullOrEmpty(path))
                if (required)
                    throw new CommandException($"The `{TentacleVariables.CurrentDeployment.PackageFilePath}` was not specified or blank. This is likely a bug in Octopus, please contact Octopus support.");
                else
                    return null;
            
            path = CrossPlatform.ExpandPathEnvironmentVariables(path);
            if (!fileSystem.FileExists(path))
                throw new CommandException("Could not find package file: " + path);

            return new PathToPackage(path);
        }
    }
}