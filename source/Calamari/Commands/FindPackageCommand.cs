using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.FileSystem;

namespace Calamari.Commands
{
    [Command("find-package", Description = "Finds the package that matches the specified ID and version. If no exact match is found, it returns a list of the nearest packages that matches the ID")]
    public class FindPackageCommand : Command
    {
        readonly PackageFindService findService;
        readonly PackageFindOptions options;

        public FindPackageCommand(ILog log, IPackageStore packageStore)
        {
            this.findService = new PackageFindService(log, packageStore);
            this.options = new PackageFindOptions();

            PackageFindOptions.ConfigureOptions(Options, options);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            findService.FindPackage(options);

            return 0;
        }
    }
}