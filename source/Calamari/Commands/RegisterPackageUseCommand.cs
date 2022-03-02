using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Versioning;

namespace Calamari.Commands
{
    [Command("register-package", Description = "Register the use of a package in the package journal")]
    public class RegisterPackageUseCommand : Command
    {
        PackageId packageId;
        VersionFormat versionFormat;
        IVersion packageVersion;
        PackagePath packagePath;
        ServerTaskId taskId;
        readonly ILog log;
        readonly IManagePackageCache journal;
        readonly ICalamariFileSystem fileSystem;

        public RegisterPackageUseCommand(ILog log, IManagePackageCache journal, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.journal = journal;
            this.fileSystem = fileSystem;
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

        public override int Execute(string[] commandLineArguments)
        {
            try
            {
                Options.Parse(commandLineArguments);
                RegisterPackageUse();
            }
            catch (Exception ex)
            {
                log.Info($"Unable to register package use.{Environment.NewLine}{ex.ToString()}");
                return ConsoleFormatter.PrintError(log, ex); // todo: what should the impact of failure here be?
            }

            return 0;
        }

        void RegisterPackageUse()
        {
            var package = new PackageIdentity(packageId, packageVersion, packagePath);
            var size = fileSystem.GetFileSize(package.Path.Value);
            journal.RegisterPackageUse(package, taskId, (ulong)size);
        }
    }
}