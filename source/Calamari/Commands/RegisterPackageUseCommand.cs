using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("register-package", Description = "Register the use of a package in the package journal")]
    public class RegisterPackageUseCommand : Command
    {
        string packageId;
        string packageVersion;
        string taskId;
        readonly ILog log;
        readonly IVariables variables;
        readonly IManagePackageUse journal;
        readonly PackageIdentityFactory packageIdentityFactory;

        public RegisterPackageUseCommand(ILog log, IManagePackageUse journal, PackageIdentityFactory packageIdentityFactory, IVariables variables)
        {
            this.log = log;
            this.journal = journal;
            this.packageIdentityFactory = packageIdentityFactory;
            this.variables = variables;
            Options.Add("packageId=", "Package ID of the used package", v => packageId = v);
            Options.Add("packageVersion=", "Package version of the used package", v => packageVersion = v);
            Options.Add("taskId=", "Id of the task that is using the package", v => taskId = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            try
            {
                Options.Parse(commandLineArguments);
                RegisterPackageUse(commandLineArguments);
            }
            catch (Exception ex)
            {
                log.Info($"Unable to register package use.{Environment.NewLine}{ex.ToString()}");
                return ConsoleFormatter.PrintError(log, ex); // todo: what should the impact of failure here be?
            }

            return 0;
        }

        void RegisterPackageUse(string[] commandLineArguments)
        {
            var deploymentTaskId = new ServerTaskId(taskId);
            var package = packageIdentityFactory.CreatePackageIdentity(journal,
                                                                       variables,
                                                                       commandLineArguments,
                                                                       VersionFormat.Semver,
                                                                       packageId,
                                                                       packageVersion);

            journal.RegisterPackageUse(package, deploymentTaskId);
        }
    }
}