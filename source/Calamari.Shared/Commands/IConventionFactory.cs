using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Commands
{
    public interface IConventionFactory
    {
        IInstallConvention SubstituteInFilesBasedOnVariableValues();
        IInstallConvention SubstituteInFiles(Func<RunningDeployment, IEnumerable<string>> fileTargetFactory);
    }
}