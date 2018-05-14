using System;
using Calamari.Integration.Processes;

namespace Calamari.Deployment
{
    public class RunningDeployment
    {
        private readonly string packageFilePath;
        private readonly CalamariVariableDictionary variables;

        public RunningDeployment(string packageFilePath, CalamariVariableDictionary variables)
        {
            this.packageFilePath = packageFilePath;
            this.variables = variables;
        }

        public string PackageFilePath
        {
            get { return packageFilePath; }
        }

        /// <summary>
        /// Gets the directory that Tentacle extracted the package to.
        /// </summary>
        public string StagingDirectory
        {
            get { return Variables.Get(SpecialVariables.OriginalPackageDirectoryPath); }
            set { Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, value); }
        }

        /// <summary>
        /// Gets the custom installation directory for this package, as selected by the user.
        /// If the user didn't choose a custom directory, this will return <see cref="StagingDirectory"/> instead.
        /// </summary>
        public string CustomDirectory
        {
            get
            {
                var custom = Variables.Get(SpecialVariables.Package.CustomInstallationDirectory);
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

        public CalamariVariableDictionary Variables
        {
            get {  return variables; }
        }

        public bool SkipJournal { get { return variables.GetFlag(SpecialVariables.Action.SkipJournal); } }

        public void Error(Exception ex)
        {
            ex = ex.GetBaseException();
            variables.Set(SpecialVariables.LastError, ex.ToString());
            variables.Set(SpecialVariables.LastErrorMessage, ex.Message);
        }
    }
}