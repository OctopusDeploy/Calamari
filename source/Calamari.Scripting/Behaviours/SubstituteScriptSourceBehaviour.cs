using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Scripting
{
    public class SubstituteScriptSourceBehaviour : IPreDeployBehaviour
    {
        ISubstituteInFiles substituteInFiles;

        public SubstituteScriptSourceBehaviour(ISubstituteInFiles substituteInFiles)
        {
            this.substituteInFiles = substituteInFiles;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.Substitute(context.CurrentDirectory, ScriptFileTargetFactory(context).ToList());

            return this.CompletedTask();
        }

        IEnumerable<string> ScriptFileTargetFactory(RunningDeployment context)
        {
            var scriptFile = context.Variables.Get(ScriptVariables.ScriptFileName);
            if (scriptFile == null)
                throw new InvalidOperationException($"{ScriptVariables.ScriptFileName} variable value could not be found.");
            yield return Path.Combine(context.CurrentDirectory, scriptFile);
        }


        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}