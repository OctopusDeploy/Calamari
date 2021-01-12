using System;
using System.Collections.Generic;
using Calamari.Common.Commands;

namespace Calamari.Common.Features.Substitutions
{
    public interface ISubstituteInFiles
    {
        void SubstituteBasedSettingsInSuppliedVariables(RunningDeployment deployment);
        void Substitute(RunningDeployment deployment, IList<string> filesToTarget, bool warnIfFileNotFound = true);
    }
}