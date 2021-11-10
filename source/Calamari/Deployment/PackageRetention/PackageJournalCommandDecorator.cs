using System;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageJournalCommandDecorator : ICommandWithArgs
    {
        readonly ILog log;
        readonly ICommandWithArgs command;
        readonly IManagePackageUse journal;
        readonly bool retentionEnabled = false;

        PackageIdentity Package { get; }
        ServerTaskId DeploymentTaskId { get; }

        public PackageJournalCommandDecorator(ILog log, ICommandWithArgs command, IVariables variables, IManagePackageUse journal)
        {
            this.log = log;
            this.command = command;
            this.journal = journal;

            retentionEnabled = variables.IsPackageRetentionEnabled();

            if (retentionEnabled)
            {
                try
                {
                    DeploymentTaskId = new ServerTaskId(variables);
                    Package = new PackageIdentity(variables);
                }
                catch (Exception ex)
                {
                    log.Error($"Unable to get deployment details for retention from variables.{Environment.NewLine}{ex.ToString()}");
                }
            }

#if DEBUG
            log.Verbose($"Decorating {command.GetType().Name} with PackageJournalCommandDecorator.");
#endif
        }

        public int Execute(string[] commandLineArguments)
        {
            if (retentionEnabled) journal.RegisterPackageUse(Package, DeploymentTaskId);

            return command.Execute(commandLineArguments);
        }
    }
}