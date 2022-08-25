using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Features.Java
{
    /// <summary>
    /// A base class for features that run Java against the Octopus Deploy 
    /// Java library
    /// </summary>
    public class JavaRunner
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly IVariables variables;

        public JavaRunner(ICommandLineRunner commandLineRunner, IVariables variables)
        {
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
        }

        /// <summary>
        /// Execute java running the Octopus Deploy Java library
        /// </summary>
        public void Run(string mainClass, Dictionary<string, string> environmentVariables)
        {
            var javaLib = variables.Get(SpecialVariables.Action.Java.JavaLibraryEnvVar, "");
            var result = commandLineRunner.Execute(
                new CommandLineInvocation(
                    JavaRuntime.CmdPath,
                    $"-Djdk.logger.finder.error=QUIET -cp calamari.jar {mainClass}"
                )
                {
                    WorkingDirectory = Path.Combine(javaLib, "contentFiles", "any", "any"),
                    EnvironmentVars = environmentVariables
                }
            );

            result.VerifySuccess();
        }
    }
}