using System.Collections.Generic;
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
        private string scriptParametersArg;
        private DeploymentJournal journal;
        private RunningDeployment deployment;
        private readonly CalamariVariableDictionary variables;
        private readonly CombinedScriptEngine scriptEngine;

        public RunScriptCommand(
            CalamariVariableDictionary variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("package=", "Path to the package to extract that contains the script.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
            Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);
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

            ValidateArguments();
            var scriptFilePath = DetermineScriptFilePath(variables);
            
            GrabAdditionalPackages(variables, filesystem);

            if (WasProvided(packageFile))
            {
                return ExecuteScriptFromPackage(scriptFilePath);
            }

            var scriptBody = variables.Get(SpecialVariables.Action.Script.ScriptBody);
            if (WasProvided(scriptBody))
            {
                return ExecuteScriptFromVariables(scriptFilePath, scriptBody);
            }

            if (WasProvided(scriptFileArg))
            {
                // Fallback for old argument usage. Use the file path provided by the calamari arguments
                // Variable substitution will not take place since we dont want to modify the file provided
                return ExecuteScriptFromParameters();
        }

            throw new CommandException("No script details provided.\r\n" +
                                       $"Pleave provide the script either via the `{SpecialVariables.Action.Script.ScriptBody}` variable, " +
                                       "through a package provided via the `--package` argument or directly via the `--script` argument.");
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

        private int ExecuteScriptFromParameters()
        {
            var scriptFilePath = Path.GetFullPath(scriptFileArg);
            InvokeScript(scriptFilePath, variables);
            return InvokeScript(scriptFilePath, variables);
        }

        private int ExecuteScriptFromVariables(string scriptFilePath, string scriptBody)
        {
            using (new TemporaryFile(scriptFilePath))
            {
                File.WriteAllText(scriptFilePath, scriptBody);
                SubstituteVariablesInScript(scriptFilePath, this.variables);
                return InvokeScript(scriptFilePath, variables);
            }
        }

        private int ExecuteScriptFromPackage(string scriptFilePath)
        {
            ExtractScriptFromPackage(variables);
            SubstituteVariablesInScript(scriptFilePath, variables);
            return InvokeScript(scriptFilePath, variables);
        }

        private void ValidateArguments()
        {
            if (WasProvided(scriptFileArg))
            {
                if (WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptBody)))
                {
                    Log.Warn(
                        $"The `--script` parameter and `{SpecialVariables.Action.Script.ScriptBody}` variable are both set." +
                        $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }

                if (WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptFileName)))
                {
                    Log.Warn(
                        $"The `--script` parameter and `{SpecialVariables.Action.Script.ScriptFileName}` variable are both set." +
                        $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }

                Log.Warn($"The `--script` parameter is depricated.\r\n" +
                         $"Please set the `{SpecialVariables.Action.Script.ScriptBody}` and `{SpecialVariables.Action.Script.ScriptFileName}` variable to allow for variable replacement of the script file.");
            }
        }

        private string DetermineScriptFilePath(VariableDictionary variables)
        {
            var scriptFileName = variables.Get(SpecialVariables.Action.Script.ScriptFileName);

            if (!WasProvided(scriptFileName) && !WasProvided(scriptFileArg))
            {
                scriptFileName = "Script."+ variables.GetEnum(SpecialVariables.Action.Script.Syntax, ScriptSyntax.Powershell).FileExtension();
            }

            return Path.GetFullPath(WasProvided(scriptFileName) ? scriptFileName : scriptFileArg);
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
            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            if (WasProvided(scriptParametersArg) && WasProvided(scriptParameters))
            {
                    Log.Warn($"The `--scriptParameters` parameter and `{SpecialVariables.Action.Script.ScriptParameters}` variable are both set.\r\n" +
                             $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
            }
            
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            Log.VerboseFormat("Executing '{0}'", scriptFileName);
            var result = scriptEngine.Execute(new Script(scriptFileName, scriptParametersArg ?? scriptParameters), variables, runner);
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

        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
        
        void GrabAdditionalPackages(VariableDictionary variables, CalamariPhysicalFileSystem fileSystem)
        {
            var additionalPackageKeys = variables.GetIndexes(SpecialVariables.Packages.PackageCollection);

            foreach (var key in additionalPackageKeys)
            {
                var packageOriginalPath = Path.GetFullPath(variables.Get(SpecialVariables.Packages.OriginalPath(key))); 
                var shouldExtract = variables.GetFlag(SpecialVariables.Packages.Extract(key));

                if (shouldExtract)
                {
                    var sanitizedSubDirectory = fileSystem.RemoveInvalidFileNameChars(key);
                    var extractionPath = Path.Combine(Environment.CurrentDirectory, sanitizedSubDirectory);
                    Log.Verbose($"Extracting package '{packageOriginalPath}' to '{extractionPath}'");
                    var extractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();
                    extractor.GetExtractor(packageOriginalPath).Extract(packageOriginalPath, extractionPath, true);
                    // ToDo: set extracted location variable
                }
                else
                {
                    Log.Verbose($"Copying package: '{packageOriginalPath}' -> '{Environment.CurrentDirectory}'");
                    fileSystem.CopyFile(packageOriginalPath, Environment.CurrentDirectory);
                    // ToDo: set copied location variable
                }
            }
        }

        private bool CanWriteJournal(VariableDictionary variables)
        {
            return variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
        }
    }
}
