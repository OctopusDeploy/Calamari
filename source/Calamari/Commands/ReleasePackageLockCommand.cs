using System;
using System.Globalization;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
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
        readonly IManagePackageUse packageJournal;
        readonly TimeSpan timeBeforePackageExpiration;

        string packageId;
        string packageVersion;

        public ReleasePackageLockCommand(IVariables variables, IManagePackageUse packageJournal, ILog log)
        {
            this.variables = variables;
            this.log = log;
            this.packageJournal = packageJournal;
            timeBeforePackageExpiration = GetTimeBeforeExpiration(variables.Get(KnownVariables.Calamari.PackageRetentionLockExpiration));
            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
        }

        TimeSpan GetTimeBeforeExpiration(string variable)
        {
            var defaultTime = TimeSpan.FromDays(14);
            if (string.IsNullOrEmpty(variable)) return defaultTime;

            try
            {
                return TimeSpan.Parse(variable);
            }
            catch
            {
                log.ErrorFormat("Failed to parse '{0}' into a valid TimeSpan. Using default of 14 days.", variable);
                return defaultTime;
            }
        }

        public override int Execute(string[] commandLineArguments)
        {
            var taskId = new ServerTaskId(variables);

            try
            {
                Options.Parse(commandLineArguments);

                Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
                Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");

                var packageIdentity = new PackageIdentity(packageId, packageVersion);

                packageJournal.DeregisterPackageUse(packageIdentity, taskId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to release lock for {0} v{1} for server task '{2}'", packageId, packageVersion, taskId);
                return ConsoleFormatter.PrintError(log, ex);
            }

            packageJournal.ExpireStaleLocks(timeBeforePackageExpiration);

            return 0;
        }
    }
}