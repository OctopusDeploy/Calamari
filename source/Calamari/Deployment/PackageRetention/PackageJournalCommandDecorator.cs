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
        readonly IVariables variables;
        readonly IManagePackageUse journal;
        readonly PackageIdentityFactory packageIdentityFactory;

        public PackageJournalCommandDecorator(ILog log, ICommandWithArgs command, IVariables variables, IManagePackageUse journal, PackageIdentityFactory packageIdentityFactory)
        {
            this.log = log;
            this.command = command;
            this.variables = variables;
            this.journal = journal;
            this.packageIdentityFactory = packageIdentityFactory;

#if DEBUG
            log.Verbose($"Decorating {command.GetType().Name} with PackageJournalCommandDecorator.");
#endif
        }

        public int Execute(string[] commandLineArguments)
        {
            // ReSharper disable once InvertIf
            if (variables.IsPackageRetentionEnabled())
            {
                try
                {
                    var deploymentTaskId = ServerTaskId.FromVariables(variables);
                    var package = packageIdentityFactory.CreatePackageIdentity(journal, variables, commandLineArguments);

                    journal.RegisterPackageUse(package, deploymentTaskId);
                }
                catch (Exception ex)
                {
                    log.Info($"Unable to register package use.{Environment.NewLine}{ex.ToString()}");
                }
            }

            return command.Execute(commandLineArguments);
        }
    }
}