using System.Collections.Generic;

namespace Calamari.Integration.ConfigurationTransforms
{
    public interface ITransformFileLocator
    {
        IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation);
    }
}