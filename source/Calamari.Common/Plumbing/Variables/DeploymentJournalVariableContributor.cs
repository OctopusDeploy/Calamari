using System;
using System.Linq;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Common.Plumbing.Variables
{
    public static class DeploymentJournalVariableContributor
    {
        public static void Contribute(ICalamariFileSystem fileSystem, IVariables variables)
        {
            var policySet = variables.Get(KnownVariables.RetentionPolicySet);
            if (string.IsNullOrWhiteSpace(policySet))
                return;

            var journal = new DeploymentJournal(fileSystem, SemaphoreFactory.Get(), variables);
            Previous(variables, journal, policySet);
            PreviousSuccessful(variables, journal, policySet);
        }

        internal static void Previous(IVariables variables, IDeploymentJournal journal, string policySet)
        {
            var previous = journal.GetLatestInstallation(policySet);

            if (previous == null)
            {
                variables.Set(TentacleVariables.PreviousInstallation.OriginalInstalledPath, "");
                variables.Set(TentacleVariables.PreviousInstallation.CustomInstallationDirectory, "");
                variables.Set(TentacleVariables.PreviousInstallation.PackageFilePath, "");
                variables.Set(TentacleVariables.PreviousInstallation.PackageVersion, "");
            }
            else
            {
                var previousPackage = previous.Packages.FirstOrDefault();

                variables.Set(TentacleVariables.PreviousInstallation.OriginalInstalledPath, previous.ExtractedTo);
                variables.Set(TentacleVariables.PreviousInstallation.CustomInstallationDirectory, previous.CustomInstallationDirectory);
                variables.Set(TentacleVariables.PreviousInstallation.PackageFilePath, previousPackage?.DeployedFrom ?? "");
                variables.Set(TentacleVariables.PreviousInstallation.PackageVersion, previousPackage?.PackageVersion ?? "");
            }
        }

        internal static void PreviousSuccessful(IVariables variables, IDeploymentJournal journal, string policySet)
        {
            var previous = journal.GetLatestSuccessfulInstallation(policySet);

            if (previous == null)
            {
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.OriginalInstalledPath, "");
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.CustomInstallationDirectory, "");
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.PackageFilePath, "");
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.PackageVersion, "");
            }
            else
            {
                var previousPackage = previous.Packages.FirstOrDefault();

                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.OriginalInstalledPath, previous.ExtractedTo);
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.CustomInstallationDirectory, previous.CustomInstallationDirectory);
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.PackageFilePath, previousPackage?.DeployedFrom ?? "");
                variables.Set(TentacleVariables.PreviousSuccessfulInstallation.PackageVersion, previousPackage?.PackageVersion ?? "");
            }
        }
    }
}