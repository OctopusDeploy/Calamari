using System;
using System.Collections.Generic;
using Calamari.Common.Commands;

namespace Calamari.Common.Features.ConfigurationTransforms
{
    public interface ITransformFileLocator
    {
        IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation, bool diagnosticLoggingEnabled, string currentDirectory);
    }
}