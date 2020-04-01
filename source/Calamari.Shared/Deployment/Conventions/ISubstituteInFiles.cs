using System.Collections.Generic;

namespace Calamari.Deployment.Conventions
{
    public interface ISubstituteInFiles
    {
        void SubstituteBasedSettingsInSuppliedVariables(RunningDeployment deployment);
        void Substitute(RunningDeployment deployment, IList<string> filesToTarget);
    }
}