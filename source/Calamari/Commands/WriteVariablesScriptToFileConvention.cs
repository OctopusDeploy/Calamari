using System;
using System.IO;
using System.Linq;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.Scripting;
using Calamari.Util;
using Octostache;

namespace Calamari.Commands
{
    public class WriteVariablesScriptToFileConvention : Shared.Commands.IConvention
    {
        private readonly IScriptRunner engine;

        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
            
        }

        public WriteVariablesScriptToFileConvention(IScriptRunner engine)
        {
            this.engine = engine;
        }
        public void Run(IExecutionContext obj)
        {
            var variables = obj.Variables;
            if (!TryGetScriptFromVariables(obj.Variables, out var scriptBody, out var relativeScriptFile, out var scriptSyntax) &&
                !WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptFileName)))
            {
                throw new CommandException($"Could not determine script to run.  Please provide either a `{SpecialVariables.Action.Script.ScriptBody}` variable, " + 
                                           $"or a `{SpecialVariables.Action.Script.ScriptFileName}` variable."); 
            }

            if (WasProvided(scriptBody))
            {
                var scriptFile = Path.GetFullPath(relativeScriptFile);
                
                //Set the name of the script we are about to create to the variables collection for replacement later on
                variables.Set(SpecialVariables.Action.Script.ScriptFileName, relativeScriptFile);
                
                // If the script body was supplied via a variable, then we write it out to a file.
                // This will be deleted with the working directory.
                // Bash files need SheBang as first few characters. This does not play well with BOM characters
                var scriptBytes = scriptSyntax == ScriptSyntax.Bash
                    ? scriptBody.EncodeInUtf8NoBom()
                    : scriptBody.EncodeInUtf8Bom();
                File.WriteAllBytes(scriptFile, scriptBytes);
            }
        }

        bool TryGetScriptFromVariables(VariableDictionary variables, out string scriptBody, out string scriptFileName, out ScriptSyntax syntax)
        {
            scriptBody = variables.GetRaw(SpecialVariables.Action.Script.ScriptBody);
            if (WasProvided(scriptBody))
            {
                var scriptSyntax = variables.Get(SpecialVariables.Action.Script.Syntax);
                if (scriptSyntax == null)
                {
                    syntax = engine.GetSupportedTypes().FirstOrDefault();
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
            foreach (var supportedSyntax in engine.GetSupportedTypes())
            {
                scriptBody = variables.GetRaw(SpecialVariables.Action.Script.ScriptBodyBySyntax(supportedSyntax));
                if (scriptBody == null)
                {
                    continue;
                }

                scriptFileName = "Script." + ScriptTypeExtensions.FileExtension(supportedSyntax);
                syntax = supportedSyntax;
                return true;
            }

            scriptBody = null;
            syntax = 0;
            scriptFileName = null;
            return false;
        }

    }
}