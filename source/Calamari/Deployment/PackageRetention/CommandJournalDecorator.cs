using System;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention
{
    public class CommandJournalDecorator : ICommandWithArgs
    {
        readonly ILog log;
        readonly ICommandWithArgs command;
        readonly IJournal journal;
        readonly bool retentionEnabled = false;

        PackageIdentity Package { get; }
        ServerTaskID DeploymentTaskID { get; }

        public CommandJournalDecorator(ILog log, ICommandWithArgs command, IVariables variables, IJournal journal)
        {
            this.log = log;
            this.command = command;
            this.journal = journal;

            retentionEnabled = variables.IsPackageRetentionEnabled();

            if (retentionEnabled)
            {
                try
                {
                    DeploymentTaskID = new ServerTaskID(variables);
                    Package = new PackageIdentity(variables);
                }
                catch (Exception ex)
                {
                    log.Error($"Unable to get deployment details for retention from variables.{Environment.NewLine}{ex.ToString()}");
                }
            }

#if DEBUG
            log.Verbose($"Decorating {command.GetType().Name} with command journal.");
#endif
        }

        public int Execute(string[] commandLineArguments)
        {
            if (retentionEnabled) journal.RegisterPackageUse(Package, DeploymentTaskID);

            return command.Execute(commandLineArguments);
        }
    }
}