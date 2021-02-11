using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Commands
{
    [Command("extract-package")]
    public class ExtractPackageCommand : Command
    {
        readonly ILog log;

        public ExtractPackageCommand(ILog log)
        {
            this.log = log;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
            log.Info("Running extract package");

            return 0;
        }
    }
}