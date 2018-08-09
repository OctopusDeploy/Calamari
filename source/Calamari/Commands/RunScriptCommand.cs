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
using System.Linq;
using Calamari.Modules;
using Calamari.Shared;
using Calamari.Shared.Scripting;
using Calamari.Util;
using Script = Calamari.Integration.Scripting.Script;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunScriptCommand : Command
    {
        private static readonly IVariableDictionaryUtils VariableDictionaryUtils = new VariableDictionaryUtils();
        private string scriptFileArg;
        private string packageFile;
        private string scriptParametersArg;
        private DeploymentJournal journal = null;
        private RunningDeployment deployment = null;
        private readonly CalamariVariableDictionary variables;
        private readonly CombinedScriptEngine scriptEngine;

        public RunScriptCommand(
            CalamariVariableDictionary variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("package=", "Path to the package to extract that contains the package.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
            Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);
            VariableDictionaryUtils.PopulateOptions(Options);
            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
//            Options.Parse(commandLineArguments);
//
//            variables.EnrichWithEnvironmentVariables();
//            variables.LogVariables();
//
//            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
//            var semaphore = SemaphoreFactory.Get();
//            journal = new DeploymentJournal(filesystem, semaphore, variables);
//            deployment = new RunningDeployment(packageFile, (CalamariVariableDictionary)variables);
//
//            ValidateArguments();
//
//            var result = ExecuteScriptFromPackage() ??
//                         ExecuteScriptFromVariables() ??
//                         ExecuteScriptFromParameters();
//
//            if (result.HasValue)
//            {
//                return result.Value;
//            }
//
//            throw new CommandException("No script details provided.\r\n" +
//                                       $"Pleave provide the script either via the `{SpecialVariables.Action.Script.ScriptBody}` variable, " +
//                                       "through a package provided via the `--package` argument or directly via the `--script` argument.");
            return -1;
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


        /// <summary>
        /// Fallback for old argument usage. Use the file path provided by the calamari arguments
        /// Variable substitution will not take place since we dont want to modify the file provided
        /// </summary>
        /// <returns></returns>
        private int? ExecuteScriptFromParameters()
        {
            if (!WasProvided(scriptFileArg))
            {
                return null;
            }

            var scriptFilePath = Path.GetFullPath(scriptFileArg);
            return InvokeScript(scriptFilePath, variables);
        }

        private int? ExecuteScriptFromVariables()
        {
            if (!TryGetScriptFromVariables(out var scriptBody, out var scriptFileName, out var syntax))
            {
                return null;
            }
            var fullPath = Path.GetFullPath(scriptFileName);
            
            using (new TemporaryFile(fullPath))
            {
                //Bash files need SheBang as first few characters. This does not play well with BOM characters
                var scriptBytes = syntax == ScriptSyntax.Bash
                    ? scriptBody.EncodeInUtf8NoBom()
                    : scriptBody.EncodeInUtf8Bom();
                File.WriteAllBytes(fullPath, scriptBytes);

                return InvokeScript(fullPath, variables);
            }
        }

        private bool TryGetScriptFromVariables(out string scriptBody, out string scriptFileName, out ScriptSyntax syntax)
        {
            scriptBody = variables.Get(SpecialVariables.Action.Script.ScriptBody);
            if (WasProvided(scriptBody))
            {
                scriptFileName = variables.Get(SpecialVariables.Action.Script.ScriptFileName);
                if (WasProvided(scriptFileName))
                {
                    syntax = ScriptTypeExtensions.FileNameToScriptType(scriptFileName);
                 
                    return true;
                }

                var scriptSyntax = variables.Get(SpecialVariables.Action.Script.Syntax);
                if (scriptSyntax == null)
                {
                    syntax = scriptEngine.GetSupportedTypes().FirstOrDefault();
                    Log.Warn($"No script syntax provided. Defaulting to first known supported type {syntax}");
                }
                else if (!Enum.TryParse(scriptSyntax, out syntax))
                {
                    throw new CommandException($"Unknown script syntax `{scriptSyntax}` provided");
                }

                scriptFileName = "Script." + syntax.FileExtension();
                return true;
            }

            // Try get any supported script body variable
            foreach (var supportedSyntax in scriptEngine.GetSupportedTypes())
            {
                scriptBody = variables.Get(SpecialVariables.Action.Script.ScriptBodyBySyntax(supportedSyntax));
                if (scriptBody == null)
                {
                    continue;
                }

                scriptFileName = "Script." + supportedSyntax.FileExtension();
                syntax = supportedSyntax;
                return true;
            }

            scriptBody = null;
            syntax = 0;
            scriptFileName = null;
            return false;
        }

        private int? ExecuteScriptFromPackage()
        {
            if (!WasProvided(packageFile))
            {
                return null;
            }

            var scriptFilePath = DetermineScriptFilePath(variables);
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

        ScriptSyntax DetermineSyntax(VariableDictionary variables)
        {
            var scriptFileName = variables.Get(SpecialVariables.Action.Script.ScriptFileName);
            if (WasProvided(scriptFileName) && Enum.TryParse(Path.GetExtension(scriptFileName), out ScriptSyntax fileNameSyntax))
            {
                return fileNameSyntax;
            }

            if (WasProvided(scriptFileArg) && Enum.TryParse(Path.GetExtension(scriptFileArg), out ScriptSyntax fileArgSyntax))
            {
                return fileArgSyntax;
            }

            return variables.GetEnum(SpecialVariables.Action.Script.Syntax, ScriptSyntax.PowerShell);
        }

        private string DetermineScriptFilePath(VariableDictionary variables)
        {
            var scriptFileName = variables.Get(SpecialVariables.Action.Script.ScriptFileName);

            if (!WasProvided(scriptFileName) && !WasProvided(scriptFileArg))
            {
                scriptFileName = "Script."+ DetermineSyntax(variables).FileExtension();
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
        }

        private void SubstituteVariablesInAdditionalFiles()
        {
            // Replace variables on any other files that may have been extracted with the package
            var substituter = new FileSubstituter(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
//            new SubstituteInFilesConvention(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), substituter)
//                .Install(deployment);
        }

        private int InvokeScript(string scriptFileName, CalamariVariableDictionary variables)
        {

            // Any additional files extracted from the packages or sent by the action handler are processed here
            SubstituteVariablesInAdditionalFiles();

            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            if (WasProvided(scriptParametersArg) && WasProvided(scriptParameters))
            {
                    Log.Warn($"The `--scriptParameters` parameter and `{SpecialVariables.Action.Script.ScriptParameters}` variable are both set.\r\n" +
                             $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
            }
            
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            Log.VerboseFormat("Executing '{0}'", scriptFileName);
            var result = scriptEngine.Execute(new Shared.Scripting.Script(scriptFileName, scriptParametersArg ?? scriptParameters));

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

        private bool CanWriteJournal(VariableDictionary variables)
        {
            return variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
        }
    }
}
