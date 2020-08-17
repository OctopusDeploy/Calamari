using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Common.Features.Behaviours
{
    class ExtractBehaviour: IPackageExtractionBehaviour
    {
        readonly IExtractPackage extractPackage;

        public ExtractBehaviour(IExtractPackage extractPackage)
        {
            this.extractPackage = extractPackage;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return !string.IsNullOrWhiteSpace(context.PackageFilePath);
        }

        public Task Execute(RunningDeployment context)
        {
            extractPackage.ExtractToStagingDirectory(context.PackageFilePath);
            return this.CompletedTask();
        }
    }
}