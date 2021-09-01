using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Scripting
{
    class WriteVariablesToFileBehaviour : IBeforePackageExtractionBehaviour
    {
        readonly IVariables variables;
        readonly IScriptEngine scriptEngine;

        public WriteVariablesToFileBehaviour(IVariables variables, IScriptEngine scriptEngine)
        {
            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            if (!TryGetScriptFromVariables(out var scriptBody, out var relativeScriptFile, out var scriptSyntax) && !WasProvided(variables.Get(ScriptVariables.ScriptFileName)))
                throw new CommandException($"Could not determine script to run.  Please provide either a `{ScriptVariables.ScriptBody}` variable, " + $"or a `{ScriptVariables.ScriptFileName}` variable.");

            if (WasProvided(scriptBody) && relativeScriptFile is not null)
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

            return this.CompletedTask();
        }

        bool TryGetScriptFromVariables([NotNullWhen(true)]
                                       out string? scriptBody,
                                       [NotNullWhen(true)]
                                       out string? scriptFileName,
                                       out ScriptSyntax syntax)
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
                    continue;

                scriptFileName = "Script." + supportedSyntax.FileExtension();
                syntax = supportedSyntax;
                return true;
            }

            scriptBody = null;
            syntax = 0;
            scriptFileName = null;
            return false;
        }

        bool WasProvided([NotNullWhen(true)]
                         string? value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}