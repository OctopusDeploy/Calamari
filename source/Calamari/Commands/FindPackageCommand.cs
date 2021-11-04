using System;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.FileSystem;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("find-package", Description = "Finds the package that matches the specified ID and version. If no exact match is found, it returns a list of the nearest packages that matches the ID")]
    public class FindPackageCommand : Command
    {
        readonly ILog log;
        readonly IVariables variables;
        readonly IPackageStore packageStore;
        readonly IJournal packageJournal;
        string packageId;
        string rawPackageVersion;
        string packageHash;
        bool exactMatchOnly;
        VersionFormat versionFormat = VersionFormat.Semver;

        //TODO: move journal/variable(?) stuff to mediator
        public FindPackageCommand(ILog log, IVariables variables, IPackageStore packageStore, IJournal packageJournal)
        {
            this.log = log;
            this.variables = variables;
            this.packageStore = packageStore;
            this.packageJournal = packageJournal;

            Options.Add("packageId=", "Package ID to find", v => packageId = v);
            Options.Add("packageVersion=", "Package version to find", v => rawPackageVersion = v);
            Options.Add("packageHash=", "Package hash to compare against", v => packageHash = v);
            Options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out VersionFormat format))
                    {
                        throw new CommandException($"The provided version format `{format}` is not recognised.");
                    }

                    versionFormat = format;
                });
            Options.Add("exactMatch=", "Only return exact matches", v => exactMatchOnly = bool.Parse(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(rawPackageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(packageHash, "No package hash was specified. Please pass --packageHash YourPackageHash");

            var version = VersionFactory.TryCreateVersion(rawPackageVersion, versionFormat);
            if (version == null)
                throw new CommandException($"Package version '{rawPackageVersion}' is not a valid {versionFormat} version string. Please pass --packageVersionFormat with a different version type.");

            var package = packageStore.GetPackage(packageId, version, packageHash);
            if (package == null)
            {
                log.Verbose($"Package {packageId} version {version} hash {packageHash} has not been uploaded.");

                if (exactMatchOnly)
                    return 0;

                FindEarlierPackages(version);

                return 0;
            }

            //Exact package found, so we need to register use and lock it.  We don't lock on partial finds, because there may be too many packages, blocking retention later,
            //  and we can lock them on apply delta anyway.
            if (PackageRetentionState.Enabled) packageJournal.RegisterPackageUse(variables);

            log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.PackageId, package.Version, package.Hash);
            LogPackageFound(
                package.PackageId,
                package.Version,
                package.Hash,
                package.Extension,
                package.FullFilePath,
                true
            );
            return 0;
        }

        void FindEarlierPackages(IVersion version)
        {
            log.VerboseFormat("Finding earlier packages that have been uploaded to this Tentacle.");
            var nearestPackages = packageStore.GetNearestPackages(packageId, version).ToList();
            if (!nearestPackages.Any())
            {
                log.VerboseFormat("No earlier packages for {0} has been uploaded", packageId);
            }

            log.VerboseFormat("Found {0} earlier {1} of {2} on this Tentacle",
                nearestPackages.Count, nearestPackages.Count == 1 ? "version" : "versions", packageId);
            foreach (var nearestPackage in nearestPackages)
            {
                log.VerboseFormat("  - {0}: {1}", nearestPackage.Version, nearestPackage.FullFilePath);
                LogPackageFound(
                    nearestPackage.PackageId,
                    nearestPackage.Version,
                    nearestPackage.Hash,
                    nearestPackage.Extension,
                    nearestPackage.FullFilePath,
                    false
                );
            }
        }


        public void LogPackageFound(
            string packageId,
            IVersion packageVersion,
            string packageHash,
            string packageFileExtension,
            string packageFullPath,
            bool exactMatchExists
        )
        {
            if (exactMatchExists)
                log.Verbose("##octopus[calamari-found-package]");

            log.VerboseFormat("##octopus[foundPackage id=\"{0}\" version=\"{1}\" versionFormat=\"{2}\" hash=\"{3}\" remotePath=\"{4}\" fileExtension=\"{5}\"]",
                AbstractLog.ConvertServiceMessageValue(packageId),
                AbstractLog.ConvertServiceMessageValue(packageVersion.ToString()),
                AbstractLog.ConvertServiceMessageValue(packageVersion.Format.ToString()),
                AbstractLog.ConvertServiceMessageValue(packageHash),
                AbstractLog.ConvertServiceMessageValue(packageFullPath),
                AbstractLog.ConvertServiceMessageValue(packageFileExtension));
        }
    }
}