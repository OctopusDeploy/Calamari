using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Util;
using Octostache;

namespace Calamari.Deployment.Conventions
{
    public class TransferPackageConvention :Calamari.Shared.Commands.IConvention
    {
        private readonly ICalamariFileSystem fileSystem;

        public TransferPackageConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Run(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            var transferPath = ExpandPathEnvironmentVariables(variables, SpecialVariables.Package.TransferPath);
                
            
            var packageFile = deployment.PackageFilePath ??
                              ExpandPathEnvironmentVariables(variables, SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);
            
            if(string.IsNullOrEmpty(packageFile))
            {
                throw new CommandException($"No package file was specified. Please provide `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    
            
            
            fileSystem.EnsureDirectoryExists(transferPath);
            var fileName = variables.Get(SpecialVariables.Package.OriginalFileName) ?? Path.GetFileName(packageFile);
            var filePath = Path.Combine(transferPath, fileName);

            if (fileSystem.FileExists(filePath))
            {
                Log.Info($"File {filePath} already exists so it will be attempted to be overwritten");
            }

            fileSystem.CopyFile(packageFile, filePath);

            Log.Info($"Copied package '{fileName}' to directory '{transferPath}'");
            Log.SetOutputVariable(SpecialVariables.Package.Output.DirectoryPath, transferPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FileName, fileName);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FilePath, filePath);
        }

        string ExpandPathEnvironmentVariables(VariableDictionary variables, string variableName)
        {
            return CrossPlatform.ExpandPathEnvironmentVariables(variables.Get(variableName));
        }
    }
}