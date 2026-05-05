using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;

namespace Calamari.Common.Util;

public class NegatingExtractionChecker : IExtractionChecker
{
    readonly IExtractionChecker extractionChecker;

    public NegatingExtractionChecker(IExtractionChecker extractionChecker)
    {
        this.extractionChecker = extractionChecker;
    }

    public bool ShouldExtractReference(RunningDeployment deployment, string referenceName)
    {
        return !extractionChecker.ShouldExtractReference(deployment, referenceName);
    }
}