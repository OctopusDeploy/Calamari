using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public abstract class TerraformCommand : Command
    {
        private readonly Func<ICalamariFileSystem, IFileSubstituter, IConvention> step;
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string packageFile;

        protected TerraformCommand(Func<ICalamariFileSystem, IFileSubstituter, IConvention> step)
        {
            this.step = step;
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            
            if (!string.IsNullOrEmpty(packageFile))
            {
                if (!fileSystem.FileExists(packageFile))
                {
                    throw new CommandException("Could not find package file: " + packageFile);
                }
            }

            fileSystem.FreeDiskSpaceOverrideInMegaBytes = variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            fileSystem.SkipFreeDiskSpaceCheck = variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);
            var substituter = new FileSubstituter(fileSystem);
            var packageExtractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(packageExtractor, fileSystem).When(_ => packageFile != null),
                step(fileSystem, substituter)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            return 0;
        }
    }
}