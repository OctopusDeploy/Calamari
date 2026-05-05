using System;
using Calamari.Common.Commands;

namespace Calamari.Common.Util;

public interface IExtractionChecker
{
    bool ShouldExtractReference(RunningDeployment deployment, string referenceName);
}