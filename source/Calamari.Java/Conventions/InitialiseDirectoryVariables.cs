using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Java.Conventions
{
    /// <summary>
    /// This convention is for steps that don't extract a package, but still need to configure
    /// directories so scripts can be run.
    /// </summary>
    public class InitialiseDirectoryVariables : IInstallConvention
    {
        public InitialiseDirectoryVariables()
        {

        }
        
        public void Install(RunningDeployment deployment)
        {
            deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);
        }
    }
}