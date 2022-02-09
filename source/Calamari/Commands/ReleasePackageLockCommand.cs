using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("release-package-lock", Description = "Releases a given package lock for a specific task ID.")]
    public class ReleasePackageLockCommand : Command
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IManagePackageUse journal;
        readonly TimeSpan defaultTimeBeforeLockExpiration = TimeSpan.FromDays(14);

        PackageId packageId;
        VersionFormat versionFormat;
        IVersion packageVersion;
        PackagePath packagePath;
        ServerTaskId taskId;

        public ReleasePackageLockCommand(IVariables variables, IManagePackageUse journal, ILog log)
        {
            this.variables = variables;
            this.log = log;
            this.journal = journal;

            Options.Add("packageId=", "Package ID of the used package", v => packageId = new PackageId(v));
            Options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                        v =>
                        {
                            if (!Enum.TryParse(v, out VersionFormat format))
                            {
                                throw new CommandException($"The provided version format `{format}` is not recognised.");
                            }
                            versionFormat = format;
                        });
            Options.Add("packageVersion=", "Package version of the used package", v => packageVersion = VersionFactory.TryCreateVersion(v, versionFormat));
            Options.Add("packagePath=", "Path to the package", v => packagePath = new PackagePath(v));
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

                Guard.NotNull(packageId, "No package ID was specified. Please pass --packageId YourPackage");
                Guard.NotNull(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");

                var packageIdentity = new PackageIdentity(packageId, packageVersion, packagePath);

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