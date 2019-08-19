using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octostache;

namespace Calamari.Deployment.Features.Java
{
    /// <summary>
    /// A base class for features that run Java against the Octopus Deploy 
    /// Java library
    /// </summary>
    public class JavaRunner
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly VariableDictionary variables;

        public JavaRunner(ICommandLineRunner commandLineRunner, VariableDictionary variables)
        {
            this.commandLineRunner = commandLineRunner;
            this.variables = variables;
        }
        
        /// <summary>
        /// Execute java running the Octopus Deploy Java library
        /// </summary>
        public void Run(string mainClass, Dictionary<string,string> environmentVariables)
        {           
            var javaLib = variables.Get(SpecialVariables.Action.Java.JavaLibraryEnvVar, "");
            var result = commandLineRunner.Execute(new CommandLineInvocation(
                JavaRuntime.CmdPath, 
                $"-Pjdk.logger.finder.error=QUIET -cp calamari.jar {mainClass}",
                Path.Combine(javaLib, "contentFiles", "any", "any"),
                environmentVariables));
            
            result.VerifySuccess();
        }
    }
    
}