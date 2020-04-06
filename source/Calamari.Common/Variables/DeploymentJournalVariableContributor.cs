using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes.Semaphores;

namespace Calamari.Variables
{
    static class DeploymentJournalVariableContributor
    {
        public static void Contribute(ICalamariFileSystem fileSystem, IVariables variables)
        {
            var policySet = variables.Get(SpecialVariables.RetentionPolicySet);
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
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.OriginalInstalledPath, "");
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.CustomInstallationDirectory, "");
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageFilePath, "");
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageVersion, "");
            }
            else
            {
                var previousPackage = previous.Packages.FirstOrDefault();
                
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.OriginalInstalledPath, previous.ExtractedTo);
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.CustomInstallationDirectory, previous.CustomInstallationDirectory);
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageFilePath, previousPackage?.DeployedFrom ?? "");
                variables.Set(SpecialVariables.Tentacle.PreviousInstallation.PackageVersion, previousPackage?.PackageVersion ?? "");
            }
        }

        internal static void PreviousSuccessful(IVariables variables, IDeploymentJournal journal, string policySet)
        {
            var previous = journal.GetLatestSuccessfulInstallation(policySet);
            
            if (previous == null)
            {
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath, "");
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory, "");
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageFilePath, "");
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageVersion, "");
            }
            else
            {
                var previousPackage = previous.Packages.FirstOrDefault();
                
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath, previous.ExtractedTo);
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory, previous.CustomInstallationDirectory);
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageFilePath, previousPackage?.DeployedFrom ?? "");
                variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageVersion, previousPackage?.PackageVersion ?? "");
            }
        }
        
    }
}