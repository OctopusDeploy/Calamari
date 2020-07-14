using System.Collections.Generic;
using Calamari.Deployment;

namespace Calamari.CommonTemp
{
    public interface ITransformFileLocator
    {
        IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation, bool diagnosticLoggingEnabled, RunningDeployment deployment);
    }
}