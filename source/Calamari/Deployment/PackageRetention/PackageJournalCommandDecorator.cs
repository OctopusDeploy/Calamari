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

        public PackageJournalCommandDecorator(ILog log, ICommandWithArgs command, IManagePackageUse journal)
        {
            this.log = log;
            this.command = command;
            this.journal = journal;

#if DEBUG
            log.Verbose($"Decorating {command.GetType().Name} with PackageJournalCommandDecorator.");
#endif
        }

        public int Execute(string[] commandLineArguments)
        {
            journal.RegisterPackageUse();

            return command.Execute(commandLineArguments);
        }
    }
}