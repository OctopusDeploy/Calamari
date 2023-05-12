using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Commands
{
    public class RunningDeployment
    {
        public delegate RunningDeployment Factory(string? packageFilePath);

        public RunningDeployment(IVariables variables, Dictionary<string, string>? environmentVariables = null) : this(
            null, variables, environmentVariables)
        {
        }

        public RunningDeployment(string? packageFilePath, IVariables variables,
            Dictionary<string, string>? environmentVariables = null)
        {
            if (!string.IsNullOrEmpty(packageFilePath))
            {
                PackageFilePath = new PathToPackage(packageFilePath);
            }

            Variables = variables;
            EnvironmentVariables = environmentVariables ?? new Dictionary<string, string>();
        }

        public PathToPackage? PackageFilePath { get; }

        /// <summary>
        /// Gets the directory that Tentacle extracted the package to.
        /// </summary>
        public string? StagingDirectory
        {
            get => Variables.Get(KnownVariables.OriginalPackageDirectoryPath);
            set => Variables.Set(KnownVariables.OriginalPackageDirectoryPath, value);
        }

        /// <summary>
        /// Gets the custom installation directory for this package, as selected by the user.
        /// If the user didn't choose a custom directory, this will return <see cref="StagingDirectory" /> instead.
        /// </summary>
        public string? CustomDirectory
        {
            get
            {
                var custom = Variables.Get(PackageVariables.CustomInstallationDirectory);
                return string.IsNullOrWhiteSpace(custom) ? StagingDirectory : custom;
            }
        }

        public DeploymentWorkingDirectory CurrentDirectoryProvider { get; set; }

        public string CurrentDirectory =>
            CurrentDirectoryProvider == DeploymentWorkingDirectory.StagingDirectory
                ? string.IsNullOrWhiteSpace(StagingDirectory)
                    ? Environment.CurrentDirectory
                    : StagingDirectory
                : CustomDirectory ?? throw new InvalidOperationException("Current directory is not set for the deployment");

        public IVariables Variables { get; }

        public Dictionary<string, string> EnvironmentVariables { get; }

        public bool SkipJournal
        {
            get => Variables.GetFlag(KnownVariables.Action.SkipJournal);
            set => Variables.Set(KnownVariables.Action.SkipJournal, value.ToString().ToLower());
        }

        public void Error(Exception ex)
        {
            ex = ex.GetBaseException();
            Variables.Set("OctopusLastError", ex.ToString());
            Variables.Set("OctopusLastErrorMessage", ex.Message);
        }
    }
}