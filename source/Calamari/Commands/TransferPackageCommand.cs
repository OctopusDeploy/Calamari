using System;
using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.PackageRetention;

namespace Calamari.Commands
{
    [Command("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageCommand : Command
    {
        readonly ILog log;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;

        public TransferPackageCommand(ILog log, IDeploymentJournalWriter deploymentJournalWriter, IVariables variables, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.fileSystem = fileSystem;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var packageFile = variables.GetPathToPrimaryPackage(fileSystem, true);
            if (packageFile == null) // required: true in the above call means it will throw rather than return null, but there's no way to tell the compiler that. And ! doesn't work in older frameworks
                throw new CommandException("Package File path could not be determined");

            var journal = new DeploymentJournal(fileSystem, new SystemSemaphoreManager(), variables);

            var conventions = new List<IConvention>
            {
                new AlreadyInstalledConvention(log, journal),
                new TransferPackageConvention(log, fileSystem),
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

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