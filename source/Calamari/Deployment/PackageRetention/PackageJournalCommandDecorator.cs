using System;
using Calamari.Commands.Support;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Commands.Options;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageJournalCommandDecorator : ICommandWithArgs
    {
        readonly ILog log;
        readonly ICommandWithArgs command;
        readonly IVariables variables;
        readonly IManagePackageUse journal;

        public PackageJournalCommandDecorator(ILog log, ICommandWithArgs command, IVariables variables, IManagePackageUse journal)
        {
            this.log = log;
            this.command = command;
            this.variables = variables;
            this.journal = journal;
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
                    var deploymentTaskId = new ServerTaskId(variables);
                    var package = PackageIdentity.GetPackageIdentity(journal, variables, commandLineArguments);

                    journal.RegisterPackageUse(package, deploymentTaskId);
                }
                catch (Exception ex)
                {
                    log.Error($"Unable to register package use.{Environment.NewLine}{ex.ToString()}");
                }
            }

            return command.Execute(commandLineArguments);
        }

        /*
        static PackageIdentity GetPackageIdentity(IManagePackageUse journal, IVariables variables, string[] commandLineArguments)
        {
            var packageStr = variables.Get(PackageVariables.PackageId) ?? throw new Exception("Package Id not found.");
            var versionStr = variables.Get(PackageVariables.PackageVersion) ?? throw new Exception("Package Version not found.");
            var packageId = new PackageId(packageStr);
            var versionFormat = VersionFormat.Semver;
            var haveDeterminedVersionFormat = false;

            //From command line args
            var customOptions = new OptionSet();
            customOptions.Add("packageVersionFormat=",
                              $"",
                              v =>
                              {
                                  haveDeterminedVersionFormat = Enum.TryParse(v, out VersionFormat format);
                                  versionFormat = format;
                              });

            customOptions.Parse(commandLineArguments);

            if (!haveDeterminedVersionFormat)
            {
                //Use package path info
                var packagePath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
                if (packagePath != null && PackageName.TryFromFile(packagePath, out var packageFileNameMetadata))
                {
                    versionFormat = packageFileNameMetadata.Version.Format;
                    haveDeterminedVersionFormat = true;
                }
            }

            if (!haveDeterminedVersionFormat)
            {
                //if we can't get the version format, try asking the journal to see if this package/task already has format info.  Otherwise assume SemVer
                if (!journal.TryGetVersionFormat(packageId, packageStr, out versionFormat))
                {
                    versionFormat = VersionFormat.Semver;
                }
            }

            var version = VersionFactory.CreateVersion(versionStr, versionFormat);
            return new PackageIdentity(packageId, version);
        }       */
    }
}