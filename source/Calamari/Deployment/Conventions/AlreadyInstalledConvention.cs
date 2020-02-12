﻿using System;
using System.Linq;
using Calamari.Deployment.Journal;

namespace Calamari.Deployment.Conventions
{
    public class AlreadyInstalledConvention : IInstallConvention
    {
        readonly IDeploymentJournal journal;

        public AlreadyInstalledConvention(IDeploymentJournal journal)
        {
            this.journal = journal;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.SkipIfAlreadyInstalled))
            {
                return;
            }

            var id = deployment.Variables.Get(SpecialVariables.Package.PackageId);
            var version = deployment.Variables.Get(SpecialVariables.Package.PackageVersion);
            var policySet = deployment.Variables.Get(SpecialVariables.RetentionPolicySet);

            var previous = journal.GetLatestInstallation(policySet, id, version);
            if (previous == null) 
                return;

            if (!previous.WasSuccessful)
            {
                Log.Info("The previous attempt to deploy this package was not successful; re-deploying.");
            }
            else
            {
                Log.Info("The package has already been installed on this machine, so installation will be skipped.");
                Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, previous.ExtractedTo);
                Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, previous.ExtractedTo);
                deployment.Variables.Set(SpecialVariables.Action.SkipRemainingConventions, "true");
                deployment.Variables.Set(SpecialVariables.Action.SkipJournal, "true");
            }
        }
    }
}
