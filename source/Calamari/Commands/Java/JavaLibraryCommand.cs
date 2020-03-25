using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features.Java;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;

namespace Calamari.Commands.Java
{
    [Command("java-library", Description = "Invokes the Octopus java library")]
    public class JavaLibraryCommand : Command
    {
        string actionType;
        readonly ScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;

        public JavaLibraryCommand(ScriptEngine scriptEngine, ICalamariFileSystem fileSystem, IVariables variables, ICommandLineRunner commandLineRunner)
        {
            Options.Add("actionType=", "The step type being invoked.", v => actionType = v);
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            JavaRuntime.VerifyExists();

            var embeddedResources = new AssemblyEmbeddedResources();
            
            var conventions = new List<IConvention>
            {
                new JavaStepConvention(actionType, new JavaRunner(commandLineRunner, variables)),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine,
                    commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(null, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}