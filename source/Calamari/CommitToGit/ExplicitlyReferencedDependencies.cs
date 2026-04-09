using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Util;

namespace Calamari.CommitToGit;

public class ExplicitlyReferencedDependencies : IExtractionChecker
{
    readonly CommitToGitDependencyMetadataParser parser;

    public ExplicitlyReferencedDependencies(CommitToGitDependencyMetadataParser parser)
    {
        this.parser = parser;
    }

    public bool ShouldExtractReference(RunningDeployment deployment, string referenceName)
    {
        var referenceNames = parser.ReferencedDependencyNames(deployment);
        return referenceNames.Contains(referenceName);
    }
}