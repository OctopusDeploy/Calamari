using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;

namespace Calamari.Commands
{
    [Command("version", Description = "Show version information")]
    public class VersionCommand : Command
    {
        public override int Execute(string[] commandLineArguments)
        {
            var assembly = GetType().Assembly;
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Console.WriteLine(fvi.FileVersion);
            return 0;
        }
    }
}
