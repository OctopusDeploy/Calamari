﻿using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Util;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a script")]
    public class RunScriptCommand : Command
    {
        string scriptFileArg;
        string packageFile;
        string scriptParametersArg;
        readonly ILog log;
        readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IVariables variables;
        readonly IScriptEngine scriptEngine;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;

        public RunScriptCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            IScriptEngine scriptEngine,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles
        )
        {
            Options.Add("package=", "Path to the package to extract that contains the script.", v => packageFile = Path.GetFullPath(v));
            Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
            Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);
            this.log = log;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.substituteInFiles = substituteInFiles;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariableReplacer = new JsonConfigurationVariableReplacer();

            ValidateArguments();
            WriteVariableScriptToFile();

            var conventions = new List<IConvention>
            {
                new StageScriptPackagesConvention(packageFile, fileSystem, new CombinedPackageExtractor(log)),
                // Substitute the script source file
                new DelegateInstallConvention(d => substituteInFiles.Substitute(d, ScriptFileTargetFactory(d).ToList())),
                // Substitute any user-specified files
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesService(fileSystem, replacer)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesService(jsonVariableReplacer, fileSystem)),
                new ExecuteScriptConvention(scriptEngine, commandLineRunner)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();
            var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
            deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, packageFile);
            return exitCode.Value;
        }

        void WriteVariableScriptToFile()
        {
            if (!TryGetScriptFromVariables(out var scriptBody, out var relativeScriptFile, out var scriptSyntax) &&
                !WasProvided(variables.Get(ScriptVariables.ScriptFileName)))
            {
                throw new CommandException($"Could not determine script to run.  Please provide either a `{ScriptVariables.ScriptBody}` variable, " +
                                           $"or a `{ScriptVariables.ScriptFileName}` variable.");
            }

            if (WasProvided(scriptBody))
            {
                var scriptFile = Path.GetFullPath(relativeScriptFile);

                //Set the name of the script we are about to create to the variables collection for replacement later on
                variables.Set(ScriptVariables.ScriptFileName, relativeScriptFile);

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
            scriptBody = variables.GetRaw(ScriptVariables.ScriptBody);
            if (WasProvided(scriptBody))
            {
                var scriptSyntax = variables.Get(ScriptVariables.Syntax);
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
                scriptBody = variables.GetRaw(SpecialVariables.Action.Script.ScriptBodyBySyntax(supportedSyntax));
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

        void ValidateArguments()
        {
            if (WasProvided(scriptFileArg))
            {
                if (WasProvided(variables.Get(ScriptVariables.ScriptBody)))
                {
                    Log.Warn(
                        $"The `--script` parameter and `{ScriptVariables.ScriptBody}` variable are both set." +
                        $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }

                if (WasProvided(variables.Get(ScriptVariables.ScriptFileName)))
                {
                    Log.Warn(
                        $"The `--script` parameter and `{ScriptVariables.ScriptFileName}` variable are both set." +
                        $"\r\nThe variable value takes precedence to allow for variable replacement of the script file.");
                }
                else
                {
                    variables.Set(ScriptVariables.ScriptFileName, scriptFileArg);
                }

                Log.Warn($"The `--script` parameter is deprecated.\r\n" +
                         $"Please set the `{ScriptVariables.ScriptBody}` and `{ScriptVariables.ScriptFileName}` variable to allow for variable replacement of the script file.");
            }

            if (WasProvided(scriptParametersArg))
            {
                if (WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptParameters)))
                {
                    Log.Warn($"The `--scriptParameters` parameter and `{SpecialVariables.Action.Script.ScriptParameters}` variable are both set.\r\n" +
                             $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
                }
                else
                {
                    variables.Set(SpecialVariables.Action.Script.ScriptParameters, scriptParametersArg);
                }
            }
        }

        IEnumerable<string> ScriptFileTargetFactory(RunningDeployment deployment)
        {
            // We should not perform variable-replacement if a file arg is passed in since this deprecated property
            // should only be coming through if something isn't using the variable-dictionary and hence will
            // have already been replaced on the server
            if (WasProvided(scriptFileArg) && !WasProvided(packageFile))
            {
                yield break;
            }

            var scriptFile = deployment.Variables.Get(ScriptVariables.ScriptFileName);
            yield return Path.Combine(deployment.CurrentDirectory, scriptFile);
        }

        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}