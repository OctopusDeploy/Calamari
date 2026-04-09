using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Util;
using Calamari.Deployment.Conventions.DependencyVariables;

namespace Calamari.Deployment.Conventions;

public class SelectiveDependencyStagingConvention : StageDependenciesConvention
{
    IExtractionChecker extractionChecker;
    
    public SelectiveDependencyStagingConvention(string? dependencyPathContainingPrimaryFiles, ICalamariFileSystem fileSystem, IPackageExtractor extractor, IDependencyVariablesFactory dependencyVariablesFactory,
                                                IExtractionChecker extractionChecker,
                                                bool forceExtract = false) : base(dependencyPathContainingPrimaryFiles, fileSystem, extractor, dependencyVariablesFactory,
                                                                                  forceExtract)
    {
        this.extractionChecker = extractionChecker;
    }

    protected override bool ShouldExtractReference(RunningDeployment deployment, string referenceName)
    {
        return extractionChecker.ShouldExtractReference(deployment, referenceName);
    }
}