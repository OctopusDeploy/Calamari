using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Octopus.Deploy.Startup;
using Octostache;

namespace Octopus.Deploy.PackageInstaller
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Usage: Octopus.Deploy.PackageInstaller.exe <nuget-package> <variables-file>");
                    return 1;
                }

                var packageFilePath = EnsureExists(MapPath(args[0]));
                var variablesFilePath = EnsureExists(MapPath(args[1]));

                var variables = new VariableDictionary(variablesFilePath);

                var conventions = new List<IConvention>
                {
                    new ExtractPackageToTemporaryDirectoryConvention(),
                    new DeployScriptConvention("PreDeploy"),
                    new DeletePackageFileConvention(),
                    new SubstituteInFilesConvention(),
                    new ConfigurationTransformsConvention(),
                    new ConfigurationVariablesConvention(),
                    new AzureConfigurationConvention(),
                    new CopyPackageToCustomInstallationDirectoryConvention(),
                    new DeployScriptConvention("Deploy"),
                    new LegacyIisWebSiteConvention(),
                    new AzureUploadConvention(),
                    new AzureDeploymentConvention(),
                    new DeployScriptConvention("PostDeploy")
                };

                var deployment = new RunningDeployment(packageFilePath, variables);
                var conventionRunner = new ConventionProcessor(deployment, conventions);
                conventionRunner.RunConventions();

                return 0;
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        static string MapPath(string path)
        {
            return Path.GetFullPath(path);
        }

        static string EnsureExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Could not find file: " + path);
            }

            return path;
        }
    }

    public class AzureDeploymentConvention : IConvention
    {
    }

    public class AzureUploadConvention : IConvention
    {
    }

    public class LegacyIisWebSiteConvention : IConvention
    {
    }

    public class CopyPackageToCustomInstallationDirectoryConvention : IConvention
    {
    }

    public class AzureConfigurationConvention : IConvention
    {
    }

    public class ConfigurationVariablesConvention : IConvention
    {
    }

    public class ConfigurationTransformsConvention : IConvention
    {
    }

    public class SubstituteInFilesConvention : IConvention
    {
    }

    public class DeletePackageFileConvention : IConvention
    {

    }

    public class DeployScriptConvention : IInstallConvention
    {
        readonly string scriptFilePrefix;

        public DeployScriptConvention(string scriptFilePrefix)
        {
            this.scriptFilePrefix = scriptFilePrefix;
        }

        public void Install(RunningDeployment deployment)
        {
            // Find the scripts by name, 
            // Based on the extension, call the appropriate script runner?
        }
    }

    public class ExtractPackageToTemporaryDirectoryConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            // Get the package file
            // Decide where to extract it (a variable for the root drive must be passed in)
            // Extract it using System.IO.Packaging
            // Store the result as a variable
        }
    }
}
