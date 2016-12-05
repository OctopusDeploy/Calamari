using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Features;
using Calamari.Features.Conventions;
using Calamari.Integration.Processes;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Commands
{
    [Command("feature-list", Description = "Lists the features available")]
    public class FeatureListCommand : Command
    {
   private string extensionsDirectory;

        public FeatureListCommand()
        {
            Options.Add("extensionsDirectory=", "Path the folder containing all extensions.",
               v => extensionsDirectory = v);
        }


        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var type = new FeatureLocator(new GenericPackageExtractor(), new PackageStore(new GenericPackageExtractor()), CalamariPhysicalFileSystem.GetPhysicalFileSystem(), extensionsDirectory);


            return 0;
        }
    }
}