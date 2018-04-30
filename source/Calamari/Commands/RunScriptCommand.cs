using System;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Util;
using Octostache;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunScriptCommand : Command
    {
        private string variablesFile;
        private string base64Variables;
        private string scriptFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string packageFile;
        private bool substituteVariables;
        private string scriptParameters;
        private DeploymentJournal journal;
        private RunningDeployment deployment;

        public RunScriptCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("base64Variables=", "JSON string containing variables.", v => base64Variables = v);
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

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword, base64Variables);
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var semaphore = SemaphoreFactory.Get();
            journal = new DeploymentJournal(filesystem, semaphore, variables);
            deployment = new RunningDeployment(packageFile, (CalamariVariableDictionary)variables);

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);

            ExtractPackage(variables);
            SubstituteVariablesInScript(variables);           
            return InvokeScript(variables);
        }

        void ExtractPackage(VariableDictionary variables)
        {
            if (string.IsNullOrWhiteSpace(packageFile))
                return;

            Log.Info("Extracting package: " + packageFile);

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);
            
            var extractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();
            extractor.GetExtractor(packageFile).Extract(packageFile, Environment.CurrentDirectory, true);            
        }

        private void SubstituteVariablesInScript(CalamariVariableDictionary variables)
        {
            if (!substituteVariables) return;

            Log.Info("Substituting variables in: " + scriptFile);

            var validatedScriptFilePath = AssertScriptFileExists();
            var substituter = new FileSubstituter(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            substituter.PerformSubstitution(validatedScriptFilePath, variables);
            
            // Replace variables on any other files that may have been extracted with the package
            new SubstituteInFilesConvention(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), substituter)
                .Install(deployment);
        }

        private int InvokeScript(CalamariVariableDictionary variables)
        {
            var validatedScriptFilePath = AssertScriptFileExists();

            var scriptEngine = new CombinedScriptEngine();
            var runner = new CommandLineRunner(
                new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            Log.VerboseFormat("Executing '{0}'", validatedScriptFilePath);
            var result = scriptEngine.Execute(new Script(validatedScriptFilePath, scriptParameters), variables, runner);
            var shouldWriteJournal = CanWriteJournal(variables) && deployment != null && !deployment.SkipJournal;

            if (result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput, false))
            {
                if(shouldWriteJournal)
                    journal.AddJournalEntry(new JournalEntry(deployment, false));

                return -1;
            }

            if (shouldWriteJournal)
                journal.AddJournalEntry(new JournalEntry(deployment, true));

            return result.ExitCode;
        }

        private string AssertScriptFileExists()
        {
            if (!File.Exists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            return scriptFile;
        }

        private bool CanWriteJournal(VariableDictionary variables)
        {
            return variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
        }
    }
}
