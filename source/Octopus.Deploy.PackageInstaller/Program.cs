using System;
using System.Collections.Generic;
using System.IO;
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
}
