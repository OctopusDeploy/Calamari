using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Shared;
using Calamari.Shared.Commands;

namespace Calamari.Commands
{
    [Command("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageCommand : Command, Shared.Commands.ICustomCommand
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public TransferPackageCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            var packageFile = variables.GetEnvironmentExpandedPath(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);
            if(string.IsNullOrEmpty(packageFile))
            {
                throw new CommandException($"No package file was specified. Please provide `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            
            var cb = new CommandBuilder(null);
            cb.AddConvention<TransferPackageConvention>();

            var cr = new CommandRunner(cb, fileSystem);
            cr.Run(new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword),  packageFile);
            
            return 0;
        }

        public ICommandBuilder Run(ICommandBuilder commandBuilder)
        {
            return commandBuilder.AddConvention<TransferPackageConvention>();
        }
    }
}
