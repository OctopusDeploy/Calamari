using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    [Command("deploy-azure-bicep-template", Description = "Deploy a Bicep template to Azure")]
    public class DeployAzureBicepTemplateCommand : PipelineCommand
    {
        // protected override IEnumerable<IPackageExtractionBehaviour> PackageExtraction(PackageExtractionResolver resolver)
        // {
        //     yield return resolver.Create<ExtractBicepTemplatePackageBehaviour>();
        // }

        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<DeployAzureBicepTemplateBehaviour>();
        }
    }

    // public class ExtractBicepTemplatePackageBehaviour(IExtractPackage extractPackage, ICalamariFileSystem fileSystem, ILog log) : IPackageExtractionBehaviour
    // {
    //     public bool IsEnabled(RunningDeployment context)
    //     {
    //         var sourceType = context.Variables.Get(SpecialVariables.Action.Azure.TemplateSource);
    //         return sourceType == "Package" && context.Variables.IsSet(SpecialVariables.Action.Package.PackageId);
    //     }
    //     
    //     public Task Execute(RunningDeployment context)
    //     {
    //         var packageId = context.Variables.Get(SpecialVariables.Action.Package.PackageId)!;
    //         var originalFullPath = Path.GetFullPath(context.Variables.Get(PackageVariables.IndexedOriginalPath(packageId))!);
    //         var sanitizedReferenceName = fileSystem.RemoveInvalidFileNameChars(packageId);
    //         var extractionPath = Path.Combine(context.CurrentDirectory, sanitizedReferenceName);
    //         ExtractDependency(originalFullPath, extractionPath);
    //         log.SetOutputVariable(SpecialVariables.Action.Package.PackageExtractedPath(sanitizedReferenceName), extractionPath, context.Variables);
    //         return Task.CompletedTask;
    //     }
    //
    //     void ExtractDependency(string file, string extractionPath)
    //     {
    //         Log.Info($"Extracting dependency '{file}' to '{extractionPath}'");
    //
    //         if (!File.Exists(file))
    //             throw new CommandException("Could not find dependency file: " + file);
    //
    //         extractPackage.ExtractToCustomDirectory(new PathToPackage(file), extractionPath);
    //     }
    // }
}