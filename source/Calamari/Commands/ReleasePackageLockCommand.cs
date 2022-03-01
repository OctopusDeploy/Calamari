using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("release-package-lock", Description = "Releases a given package lock for a specific task ID.")]
    public class ReleasePackageLockCommand : Command
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IManagePackageCache journal;
        readonly TimeSpan defaultTimeBeforeLockExpiration = TimeSpan.FromDays(14);

        ServerTaskId taskId;

        public ReleasePackageLockCommand(IVariables variables, IManagePackageCache journal, ILog log)
        {
            this.variables = variables;
            this.log = log;
            this.journal = journal;


            Options.Add("taskId=", "Id of the task that is using the package", v => taskId = new ServerTaskId(v));
        }

        TimeSpan GetTimeBeforeLockExpiration()
        {
            var expirationVariable = variables.Get(KnownVariables.Calamari.PackageRetentionLockExpiration);
            return TimeSpan.TryParse(expirationVariable, out var timeBeforeLockExpiration) ? timeBeforeLockExpiration : defaultTimeBeforeLockExpiration;
        }

        public override int Execute(string[] commandLineArguments)
        {
            try
            {
                Options.Parse(commandLineArguments);
                journal.RemoveAllLocks(taskId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to release package locks");
                return ConsoleFormatter.PrintError(log, ex);
            }

            journal.ExpireStaleLocks(GetTimeBeforeLockExpiration());

            return 0;
        }
    }
}