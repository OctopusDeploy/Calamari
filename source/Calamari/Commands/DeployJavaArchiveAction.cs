using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Deployment.Features.Java;
using Calamari.Deployment.Journal;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Java.Deployment.Conventions;
using Calamari.Shared;
using Calamari.Shared.Commands;

namespace Calamari.Commands.Java
{
    [Command("deploy-java-archive", Description = "Deploys a Java archive (.jar, .war, .ear)")]
    public class DeployJavaArchiveAction : Command, Shared.Commands.IDeploymentAction
    {
        public override int Execute(string[] commandLineArguments)
        {
            return -99;
        }

        public void Build(IDeploymentStrategyBuilder cb)
        {
            //Log.Info("Deploying:    " + archiveFile);
            var deployExploded = cb.Variables.GetFlag(SpecialVariables.Action.Java.DeployExploded);

            cb.UsesDeploymentJournal = true;

            cb.Features.Add<TomcatFeature>().Add<WildflyFeature>();

            // If we are deploying the package exploded then extract directly to the application directory.
            // Else, if we are going to re-pack, then we extract initially to a temporary directory 
            if (deployExploded)
            {
                cb.AddExtractPackageToApplicationDirectory();
            }
            else
            {
                cb.AddExtractPackageToStagingDirectory();
            }

            cb.RunPreScripts()
                .AddSubsituteInFiles()
                .AddJsonVariables()
                .AddConvention<RePackArchiveConvention>()
                .AddConvention<CopyPackageToCustomInstallationDirectoryConvention>()
                .RunDeployScripts()
                .RunPostScripts();
        }
    }
}