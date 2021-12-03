using System;
using System.Globalization;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("release-package-lock", Description = "Releases a given package lock for a specific task ID.")]
    public class ReleasePackageLockCommand : Command
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IManagePackageUse journal;

        string packageId;
        string packageVersion;

        public ReleasePackageLockCommand(IVariables variables, IManagePackageUse journal, ILog log)
        {
            this.variables = variables;
            this.log = log;
            this.journal = journal;
            Options.Add("packageId=", "Package ID to download", v => packageId = v);
            Options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            var taskId = new ServerTaskId(variables);

            try
            {
                Options.Parse(commandLineArguments);

                Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
                Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");

                var packageIdentity = PackageIdentity.CreatePackageIdentity(journal, variables, commandLineArguments, packageId, packageVersion);

                journal.DeregisterPackageUse(packageIdentity, taskId);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to release lock for {0} v{1} for server task '{2}'", packageId, packageVersion, taskId);
                return ConsoleFormatter.PrintError(log, ex);
            }

            return 0;
        }
    }
}