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
            substituteInFiles.Substitute(context, ScriptFileTargetFactory(context).ToList());

            return this.CompletedTask();
        }

        IEnumerable<string> ScriptFileTargetFactory(RunningDeployment context)
        {
            // We should not perform variable-replacement if a file arg is passed in since this deprecated property
            // should only be coming through if something isn't using the variable-dictionary and hence will
            // have already been replaced on the server
            if (WasProvided(context.Variables.Get(ScriptVariables.ScriptFileName)) && !WasProvided(context.PackageFilePath))
            {
                yield break;
            }

            var scriptFile = context.Variables.Get(ScriptVariables.ScriptFileName);
            yield return Path.Combine(context.CurrentDirectory, scriptFile);
        }


        bool WasProvided(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}