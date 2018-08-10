﻿using Calamari.Deployment.Journal;
using Calamari.Shared;
using Calamari.Shared.Commands;
﻿using System.Linq;

namespace Calamari.Deployment.Conventions
{
    public class ContributePreviousSuccessfulInstallationConvention : Calamari.Shared.Commands.IConvention
    {
        readonly IDeploymentJournal journal;

        public ContributePreviousSuccessfulInstallationConvention(IDeploymentJournal journal)
        {
            this.journal = journal;
        }

        public void Run(IExecutionContext deployment)
        {
            var policySet = deployment.Variables.Get(SpecialVariables.RetentionPolicySet);
            
            var previous = journal.GetLatestSuccessfulInstallation(policySet);
            
            string previousExtractedFrom;
            string previousExtractedTo;
            string previousVersion;
            string previousCustom;
            if (previous == null)
            {
                previousExtractedTo = previousExtractedFrom = previousVersion = previousCustom = "";
            }
            else
            {
                // This is assuming this convention is used only in steps with only one package
                var previousPackage = previous.Packages.FirstOrDefault();
                
                previousExtractedTo = previous.ExtractedTo;
                previousExtractedFrom = previousPackage?.DeployedFrom ?? "";
                previousVersion = previousPackage?.PackageVersion ?? "";
                previousCustom = previous.CustomInstallationDirectory;
            }

            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath, previousExtractedTo);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory, previousCustom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageFilePath, previousExtractedFrom);
            deployment.Variables.Set(SpecialVariables.Tentacle.PreviousSuccessfulInstallation.PackageVersion, previousVersion);
        }
    }
}