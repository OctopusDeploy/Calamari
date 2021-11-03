using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention
{
    public class CommandJournalDecorator: ICommandWithArgs
    {
        readonly ILog log;
        readonly ICommandWithArgs command;
        readonly IJournal journal;
        
        PackageIdentity Package { get; }
        ServerTaskID DeploymentID {get;}

        public CommandJournalDecorator(ILog log, ICommandWithArgs command, IVariables variables, IJournal journal)
        {
            this.log = log;
            this.command = command;
            this.journal = journal;

            DeploymentID = new ServerTaskID(variables);
            Package = new PackageIdentity(variables);

#if DEBUG
            log.Verbose($"Decorating {command.GetType().Name} with command journal.");
#endif
        }

        public int Execute(string[] commandLineArguments)
        {
            journal.RegisterPackageUse(Package, DeploymentID);
            return command.Execute(commandLineArguments);
        }
    }
}