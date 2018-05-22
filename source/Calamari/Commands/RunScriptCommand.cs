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
using Octostache;
using System;
using System.IO;
using Calamari.Modules;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunScriptCommand : Command
    {
        private static readonly IVariableDictionaryUtils VariableDictionaryUtils = new VariableDictionaryUtils();
        private string scriptFileArg;
        private string packageFile;
        private string scriptParameters;
        private DeploymentJournal journal;
        private RunningDeployment deployment;
        private readonly CalamariVariableDictionary variables;
        private readonly CombinedScriptEngine scriptEngine;

        public RunScriptCommand(
            CalamariVariableDictionary variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", "Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = Path.GetFullPath(v));
            Options.Add("scriptParameters=", "Parameters to pass to the script.", v => scriptParameters = v);
            VariableDictionaryUtils.PopulateOptions(Options);
            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var semaphore = SemaphoreFactory.Get();
            journal = new DeploymentJournal(filesystem, semaphore, variables);
            deployment = new RunningDeployment(packageFile, (CalamariVariableDictionary)variables);

            var scriptFileName = EnsureScriptReady(variables);
            return InvokeScript(scriptFileName, variables);
        }

        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }



        void ExtractScriptFromPackage(VariableDictionary variables)
        {
            Log.Info("Extracting package: " + packageFile);

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            var extractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();
            extractor.GetExtractor(packageFile).Extract(packageFile, Environment.CurrentDirectory, true);

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);
        }


        void ExtractScriptFromVariables(string scriptBody, string scriptFileName)
        {
            File.WriteAllText(scriptFileName, scriptBody);
        }

        string EnsureScriptReady(VariableDictionary variables)
        {
            var scriptBody = variables.Get(SpecialVariables.Action.Script.ScriptBody);
            var scriptFileName = variables.Get(SpecialVariables.Action.Script.ScriptFileName);


            if (WasProvided(scriptFileArg))
            {
                if (WasProvided(scriptBody))
                {
                    Log.Warn($"The `--script` parameter and `{SpecialVariables.Action.Script.ScriptBody}` variable are both set." +
                             $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }

                if (WasProvided(scriptFileName))
                {
                    Log.Warn($"The `--script` parameter and `{SpecialVariables.Action.Script.ScriptFileName}` variable are both set." +
                             $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }

                Log.Warn($"The `--script` parameter is depricated.\r\n" +
                         $"Please set the `{SpecialVariables.Action.Script.ScriptBody}` and `{SpecialVariables.Action.Script.ScriptFileName}` variable to allow for variable replacement of the script file.");
            }

            var scriptFilePath = WasProvided(scriptFileName) ? Path.GetFullPath(scriptFileName) : scriptFileArg;

            if (WasProvided(packageFile))
            {
                ExtractScriptFromPackage(variables);
                SubstituteVariablesInScript(scriptFilePath, this.variables);
            }
            else if (WasProvided(scriptBody))
            {
                ExtractScriptFromVariables(scriptBody, scriptFilePath);
                SubstituteVariablesInScript(scriptFilePath, this.variables);
            }
            else
            {
                // Fallback for old argument usage. Use the file path provided by the calamari arguments
                SubstituteVariablesInScript(scriptFileArg, this.variables);
                return scriptFileArg;
            }

            return scriptFilePath;
        }

        private void SubstituteVariablesInScript(string scriptFileName, CalamariVariableDictionary variables)
        {
            if (!File.Exists(scriptFileName))
            {
                throw new CommandException("Could not find script file: " + scriptFileName);
            }

            var substituter = new FileSubstituter(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            substituter.PerformSubstitution(scriptFileName, variables);
            
            // Replace variables on any other files that may have been extracted with the package
            new SubstituteInFilesConvention(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), substituter)
                .Install(deployment);
        }

        private int InvokeScript(string scriptFileName, CalamariVariableDictionary variables)
        {
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            Log.VerboseFormat("Executing '{0}'", scriptFileName);
            var result = scriptEngine.Execute(new Script(scriptFileName, scriptParameters), variables, runner);
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

        private bool CanWriteJournal(VariableDictionary variables)
        {
            return variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
        }
    }
}
