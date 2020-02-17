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

namespace Calamari.Commands
{
    [Command("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageCommand : Command
    {
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        private string variablesFile;
        private readonly List<string> sensitiveVariableFiles = new List<string>();
        private string sensitiveVariablesPassword;

        public TransferPackageCommand(IDeploymentJournalWriter deploymentJournalWriter)
        {
            this.deploymentJournalWriter = deploymentJournalWriter;
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariableFiles.Add(v));
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariableFiles, sensitiveVariablesPassword);
            var packageFile = variables.GetEnvironmentExpandedPath(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);
            if(string.IsNullOrEmpty(packageFile))
            {
                throw new CommandException($"No package file was specified. Please provide `{SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath}` variable");
            }

            if (!fileSystem.FileExists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            var journal = new DeploymentJournal(fileSystem, SemaphoreFactory.Get(), variables);
            
            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new ContributePreviousInstallationConvention(journal),
                new ContributePreviousSuccessfulInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new TransferPackageConvention(fileSystem),
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(deployment, true);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(deployment, false);
                throw;
            }

            return 0;
        }
    }
}
