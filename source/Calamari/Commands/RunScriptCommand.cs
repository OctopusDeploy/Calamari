using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Util;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunScriptCommand : Command
    {
        private string variablesFile;
        private string scriptFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string packageFile;
        private bool substituteVariables;
        private string scriptParameters;

        public RunScriptCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", "Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFile = Path.GetFullPath(v));
            Options.Add("scriptParameters=", "Parameters to pass to the script.", v => scriptParameters = v);
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            Options.Add("substituteVariables", "Perform variable substitution on the script body before executing it.", v => substituteVariables = true);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, CrossPlatform.GetCurrentDirectory());
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, substituteVariables.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, scriptFile);


            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var scriptCapability = new CombinedScriptEngine();
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            
            var substituter = new FileSubstituter(fileSystem);
            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention()
            };

            if (!string.IsNullOrWhiteSpace(packageFile))
            {
                conventions.Add(new ExtractPackageToWorkingDirectoryConvention(new GenericPackageExtractor(), fileSystem));
            }
            conventions.Add(new SubstituteFileConvention(scriptFile, fileSystem, substituter));
            conventions.Add(new InvokeScriptConvention(scriptFile, scriptParameters, fileSystem, scriptCapability, commandLineRunner));

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();

            return 0;
        }
    }
}