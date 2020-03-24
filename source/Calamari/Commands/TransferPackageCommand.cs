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
using Calamari.Variables;

namespace Calamari.Commands
{
    [Command("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageCommand : Command
    {
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;

        public TransferPackageCommand(IDeploymentJournalWriter deploymentJournalWriter, IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.fileSystem = fileSystem;
        }

        public override int Execute(string[] commandLineArguments)
        {
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
                new AlreadyInstalledConvention(journal),
                new DelegateInstallConvention(Install),
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
        
        void Install(RunningDeployment deployment)
        {
            var transferPath = deployment.Variables.GetEnvironmentExpandedPath(SpecialVariables.Package.TransferPath);
            fileSystem.EnsureDirectoryExists(transferPath);
            var fileName = deployment.Variables.Get(SpecialVariables.Package.OriginalFileName) ?? Path.GetFileName(deployment.PackageFilePath);
            var filePath = Path.Combine(transferPath, fileName);

            if (fileSystem.FileExists(filePath))
            {
                Log.Info($"File {filePath} already exists so it will be attempted to be overwritten");
            }

            fileSystem.CopyFile(deployment.PackageFilePath, filePath);

            Log.Info($"Copied package '{fileName}' to directory '{transferPath}'");
            Log.SetOutputVariable(SpecialVariables.Package.Output.DirectoryPath, transferPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FileName, fileName);
            Log.SetOutputVariable(SpecialVariables.Package.Output.FilePath, filePath);
        }
    }
}
