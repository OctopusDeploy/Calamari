using System;
using Calamari.Common.Variables;
using Calamari.Integration.Processes;

namespace Calamari.Deployment
{
    public class RunningDeployment
    {
        private readonly PathToPackage packageFilePath;
        private readonly IVariables variables;

        public RunningDeployment(string packageFilePath, IVariables variables)
        {
            this.packageFilePath = new PathToPackage(packageFilePath);
            this.variables = variables;
        }

        public PathToPackage PackageFilePath
        {
            get { return packageFilePath; }
        }

        /// <summary>
        /// Gets the directory that Tentacle extracted the package to.
        /// </summary>
        public string StagingDirectory
        {
            get { return Variables.Get(Common.Variables.SpecialVariables.OriginalPackageDirectoryPath); }
            set { Variables.Set(Common.Variables.SpecialVariables.OriginalPackageDirectoryPath, value); }
        }

        /// <summary>
        /// Gets the custom installation directory for this package, as selected by the user.
        /// If the user didn't choose a custom directory, this will return <see cref="StagingDirectory"/> instead.
        /// </summary>
        public string CustomDirectory
        {
            get
            {
                var custom = Variables.Get(PackageVariables.CustomInstallationDirectory);
                return string.IsNullOrWhiteSpace(custom) ? StagingDirectory : custom;
            }
        }

        public DeploymentWorkingDirectory CurrentDirectoryProvider { get; set; }

        public string CurrentDirectory
        {
            get { return CurrentDirectoryProvider == DeploymentWorkingDirectory.StagingDirectory ?
                string.IsNullOrWhiteSpace(StagingDirectory) ? Environment.CurrentDirectory : StagingDirectory
                : CustomDirectory; }
        }

        public IVariables Variables
        {
            get {  return variables; }
        }

        public bool SkipJournal { get { return variables.GetFlag(Common.Variables.SpecialVariables.Action.SkipJournal); } }

        public void Error(Exception ex)
        {
            ex = ex.GetBaseException();
            variables.Set(Common.Variables.SpecialVariables.LastError, ex.ToString());
            variables.Set(Common.Variables.SpecialVariables.LastErrorMessage, ex.Message);
        }
    }
}