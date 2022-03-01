using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;

namespace Calamari.Commands
{
    [Command("clean-packages", Description = "Apply retention to the package cache")]
    public class CleanPackagesCommand : Command
    {
        readonly IManagePackageCache journal;

        public CleanPackagesCommand(IManagePackageCache journal)
        {
            this.journal = journal;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            journal.ApplyRetention();
            return 0;
        }
    }
}