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
        readonly PackageIdentityFactory packageIdentityFactory;
        readonly IManagePackageUse journal;
        readonly TimeSpan defaultTimeBeforeLockExpiration = TimeSpan.FromDays(14);

        string packageId;
        string packageVersion;

        public ReleasePackageLockCommand(IVariables variables, IManagePackageUse journal, ILog log, PackageIdentityFactory packageIdentityFactory)
        {
            this.variables = variables;
            this.log = log;
            this.packageIdentityFactory = packageIdentityFactory;
            this.journal = journal;

            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
        }

        TimeSpan GetTimeBeforeLockExpiration()
        {
            var expirationVariable = variables.Get(KnownVariables.Calamari.PackageRetentionLockExpiration);
            return TimeSpan.TryParse(expirationVariable, out var timeBeforeLockExpiration) ? timeBeforeLockExpiration : defaultTimeBeforeLockExpiration;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var taskId = new ServerTaskId(variables);

            try
            {
                Options.Parse(commandLineArguments);

                Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
                Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");

                var packageIdentity = packageIdentityFactory.CreatePackageIdentity(journal, variables, commandLineArguments, VersionFormat.Semver, packageId, packageVersion);

                journal.DeregisterPackageUse(packageIdentity, taskId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to release lock for {0} v{1} for server task '{2}'", packageId, packageVersion, taskId);
                return ConsoleFormatter.PrintError(log, ex);
            }

            journal.ExpireStaleLocks(GetTimeBeforeLockExpiration());

            return 0;
        }
    }
}