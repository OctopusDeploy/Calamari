using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using Calamari.Kubernetes.Conventions;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Kubernetes.Commands
{
    [DeploymentActionAttribute("helm-upgrade", Description = "Performs Helm Upgrade with Chart while performing variable replacement")]
    public class HelmUpgradeDeploymentAction : IDeploymentAction
    {
        private readonly ICalamariFileSystem fileSystem;

        public HelmUpgradeDeploymentAction(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder)
        {
            deploymentStrategyBuilder
                .AddExtractPackageToStagingDirectory()
                .AddStageScriptPackages(true)
                .RunPreScripts()
                .AddSubsituteInFiles(_ => true, FileTargetFactory)
                .RunDeployScripts()
                .AddConvention<HelmUpgradeConvention>()
                .RunPostScripts();
        }

        
        private IEnumerable<string> FileTargetFactory(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(Shared.SpecialVariables.Packages.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));
                
                foreach (var path in paths)
                {
                    yield return Path.Combine(sanitizedPackageReferenceName, path);    
                }
            }
        }
    }
}