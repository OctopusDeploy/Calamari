using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment;

namespace Calamari.Commands
{
    [Command("extract-package")]
    public class ExtractPackageCommand : Command<ExtractPackageCommandInputs>
    {
        readonly IExtractPackage extractPackage;

        public ExtractPackageCommand(IExtractPackage extractPackage)
        {
            this.extractPackage = extractPackage;
        }

        protected override int Execute(ExtractPackageCommandInputs inputs)
        {
            extractPackage.ExtractToStagingDirectory(new PathToPackage(inputs.PathToPackage), inputs.ExtractedToPathOutputVariableName);

            return 0;
        }
    }

    public class ExtractPackageCommandInputs
    {
        public string PathToPackage { get; set; }
        public string ExtractedToPathOutputVariableName { get; set; }
    }
}