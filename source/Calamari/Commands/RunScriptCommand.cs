using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Octostache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Modules;
using Calamari.Util;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a script")]
    public class RunScriptCommand : Command
    {
        private static readonly IVariableDictionaryUtils VariableDictionaryUtils = new VariableDictionaryUtils();
        private string scriptFileArg;
        private string packageFile;
        private string scriptParametersArg;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        private readonly CalamariVariableDictionary variables;
        private readonly CombinedScriptEngine scriptEngine;
        private IFileSubstituter fileSubstituter; 

        public RunScriptCommand(
            IDeploymentJournalWriter deploymentJournalWriter,
            CalamariVariableDictionary variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("package=", "Path to the package to extract that contains the script.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
            Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);
            VariableDictionaryUtils.PopulateOptions(Options);
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(),
                new ServiceMessageCommandOutput(variables)));
          
            fileSubstituter = new FileSubstituter(fileSystem);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariableReplacer = new JsonConfigurationVariableReplacer();
            var extractor = new GenericPackageExtractorFactory().createStandardGenericPackageExtractor();


            ValidateArguments();

            DetermineScriptFileAndParameters(out var scriptFile, out var scriptParameters);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new StageScriptPackagesConvention(packageFile, fileSystem, extractor),
                new SubstituteInFilesConvention(fileSystem, fileSubstituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(jsonVariableReplacer, fileSystem),
                new ExecuteScriptConvention(scriptFile, scriptParameters, scriptEngine, commandLineRunner) 
            };
            
            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            
            conventionRunner.RunConventions();
            var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
            deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, packageFile);
            return exitCode.Value;
        }

        void DetermineScriptFileAndParameters(out string scriptFile, out string scriptParameters)
        {
            // There are a number of ways the script can be supplied
            
            // If the package-file argument is supplied, then the script file is within the package
            if (WasProvided(packageFile))
            {
                // The script file can be supplied either via an argument or a variable
                var relativeScriptFile = WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptFileName))
                    ? variables.Get(SpecialVariables.Action.Script.ScriptFileName)
                    : scriptFileArg;

                if (string.IsNullOrWhiteSpace(relativeScriptFile))
                    throw new CommandException("Package argument was supplied but no script file was specified.");

                scriptFile = Path.GetFullPath(relativeScriptFile);
                scriptParameters = WasProvided(scriptParametersArg)
                    ? scriptParametersArg
                    : variables.Get(SpecialVariables.Action.Script.ScriptParameters);
                
                // Automatically perform variable-substitution on the script file itself,
                // since it didn't come out of the variable dictionary
                fileSubstituter.PerformSubstitution(scriptFile, variables);
            }
            
            // A script file can be directly supplied as an argument.
            // We don't perform variable-substitution on this file, as in some cases the server has already performed it. 
            else if (WasProvided(scriptFileArg))
            {
                scriptFile = Path.GetFullPath(scriptFileArg);
                scriptParameters = null;
            }
            
            // The final way to supply a script is as source-code via a variable  
            else
            {
                if (!TryGetScriptFromVariables(out var scriptBody, out var relativeScriptFile, out var scriptSyntax))
                {
                   throw new CommandException($"Could not determine script to run.  Please provide either a `{SpecialVariables.Action.Script.ScriptBody}` variable, " + 
                    $"or a `{SpecialVariables.Action.Script.ScriptFileName}` variable."); 
                }

                scriptFile = Path.GetFullPath(relativeScriptFile);
                scriptParameters = WasProvided(scriptParametersArg)
                    ? scriptParametersArg
                    : variables.Get(SpecialVariables.Action.Script.ScriptParameters);
                
                // If the script body was supplied via a variable, then we write it out to a file.
                // This will be deleted with the working directory.
                
                // Bash files need SheBang as first few characters. This does not play well with BOM characters
                var scriptBytes = scriptSyntax == ScriptSyntax.Bash
                    ? scriptBody.EncodeInUtf8NoBom()
                    : scriptBody.EncodeInUtf8Bom();
                File.WriteAllBytes(scriptFile, scriptBytes);
            }
        }


        bool TryGetScriptFromVariables(out string scriptBody, out string scriptFileName, out ScriptSyntax syntax)
        {
            scriptBody = variables.Get(SpecialVariables.Action.Script.ScriptBody);
            if (WasProvided(scriptBody))
            {
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

                Log.Warn($"The `--script` parameter is deprecated.\r\n" +
                         $"Please set the `{SpecialVariables.Action.Script.ScriptBody}` and `{SpecialVariables.Action.Script.ScriptFileName}` variable to allow for variable replacement of the script file.");
            }
            
            if (WasProvided(scriptParametersArg) && WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptParameters)))
            {
                    Log.Warn($"The `--scriptParameters` parameter and `{SpecialVariables.Action.Script.ScriptParameters}` variable are both set.\r\n" +
                             $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
            }
        }

        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}
