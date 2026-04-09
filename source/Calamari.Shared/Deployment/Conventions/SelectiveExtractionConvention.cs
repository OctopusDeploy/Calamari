using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.Conventions.DependencyVariables;

namespace Calamari.Deployment.Conventions;

public class SelectiveExtractionConvention : StageDependenciesConvention
{
    readonly const string DependencyMetaData = "Octopus.Git.DependencyMetaData";
    
    public SelectiveExtractionConvention(string? dependencyPathContainingPrimaryFiles, ICalamariFileSystem fileSystem, IPackageExtractor extractor, IDependencyVariablesFactory dependencyVariablesFactory,
             bool forceExtract = false) : base(dependencyPathContainingPrimaryFiles, fileSystem, extractor, dependencyVariablesFactory,
                                               forceExtract)
    {
    }

    protected bool ShouldExtractReference(RunningDeployment deployment, string referenceName)
    {
        var metaData = deployment.Variables.Get(DependencyMetaData);
        return metaData.Contains(referenceName);
    }
}