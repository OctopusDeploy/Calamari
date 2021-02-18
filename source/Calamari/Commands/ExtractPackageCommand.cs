using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Commands
{
    [Command("extract-package")]
    public class ExtractPackageCommand : Command
    {
        readonly IExtractPackage extractPackage;
        PathToPackage pathToPrimaryPackage;

        public ExtractPackageCommand(IVariables variables, IExtractPackage extractPackage, ICalamariFileSystem fileSystem)
        {
            this.extractPackage = extractPackage;

            pathToPrimaryPackage = variables.GetPathToPrimaryPackage(fileSystem, true);
        }

        public override int Execute(string[] commandLineArguments)
        {
            extractPackage.ExtractToStagingDirectory(pathToPrimaryPackage);

            return 0;
        }
    }
}