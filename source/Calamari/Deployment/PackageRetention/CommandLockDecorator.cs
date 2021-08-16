using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention
{
    public class CommandJournalDecorator : ICommandWithArgs
    {
        readonly ILog log;
        readonly ICommandWithArgs command;
        readonly IVariables variables;
        readonly Journal journal;

        DeploymentID DeploymentID => new DeploymentID(variables.Get(KnownVariables.Deployment.Id));
        PackageID PackageID => new PackageID("MyPackage");   //Work out a good way of uniquely identifying the package/version

        public CommandJournalDecorator(ILog log, ICommandWithArgs command, IVariables variables, Journal journal)
        {
            this.log = log;
            this.command = command;
            this.variables = variables;
            this.journal = journal;
            
            /*
            var hasPackages = !string.IsNullOrWhiteSpace(packageFile) ||
                              deployment.Variables.GetIndexes(PackageVariables.PackageCollection).Any();

            var canWrite = deployment.Variables.Get(TentacleVariables.Agent.JournalPath) != null;*/

            log.Verbose($"Decorating {command.GetType().Name} with command journal.");
        }

        public int Execute(string[] commandLineArguments)
        {
            journal.RegisterPackageUse(PackageID, DeploymentID);
            return command.Execute(commandLineArguments);
        }
    }
}