using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Commands
{
    [Command("substitute-in-files")]
    public class SubstituteInFilesCommand : Command
    {
        readonly ILog log;

        public SubstituteInFilesCommand(ILog log)
        {
            this.log = log;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
            log.Info("Running substitute in files");

            return 0;
        }
    }
}