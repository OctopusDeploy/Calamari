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

namespace Calamari.Commands.Java
{
    [Command("deploy-java-archive", Description = "Deploys a Java archive (.jar, .war, .ear)")]
    public class DeployJavaArchiveCommand : Command
    {
        string variablesFile;
        string archiveFile;
        string sensitiveVariablesFile;
        string sensitiveVariablesPassword;
        private readonly CombinedScriptEngine scriptEngine;

        public DeployJavaArchiveCommand(CombinedScriptEngine scriptEngine)
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("archive=", "Path to the Java archive to deploy.", v => archiveFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);

            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(archiveFile, "No archive file was specified. Please pass --archive YourPackage.jar");

            if (!File.Exists(archiveFile))
                throw new CommandException("Could not find archive file: " + archiveFile);

            Log.Info("Deploying:    " + archiveFile);
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var deployExploded = variables.GetFlag(SpecialVariables.Action.Java.DeployExploded);

            var cb = new CommandBuilder(null)
                {UsesDeploymentJournal = true};

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
                
            new CommandRunner(cb, fileSystem).Run(new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword), archiveFile);
            

            return 0;
        }
    }
}