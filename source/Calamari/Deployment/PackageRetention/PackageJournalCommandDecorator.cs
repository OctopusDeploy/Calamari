using System;
using Calamari.Commands.Support;
using Calamari.Common.Features.Packages;
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




                    var version = variables.Get(PackageVariables.PackageVersion) ?? throw new Exception("Package Version not found.");

                    Package = new PackageIdentity(variables, version);
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

        void TryGetPackageIdentity(IVariables variables)
        {
            VersionFormat versionFormat;


            //Use package path info
            var packagePath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
            if (packagePath != null)
            {
                var packageFileMetadata = PackageName.FromFile(packagePath);
                versionFormat = packageFileMetadata.Version.Format;
            }

            //Use variables
            Package = new PackageIdentity(variables, version)
        }

        public int Execute(string[] commandLineArguments)
        {
            if (retentionEnabled) journal.RegisterPackageUse(Package, DeploymentTaskId);

            return command.Execute(commandLineArguments);
        }
    }
}