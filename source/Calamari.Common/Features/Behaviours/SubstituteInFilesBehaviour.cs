using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Behaviours
{
    public class SubstituteInFilesBehaviour : IBehaviour
    {
        readonly ISubstituteInFiles substituteInFiles;

        public SubstituteInFilesBehaviour(ISubstituteInFiles substituteInFiles)
        {
            this.substituteInFiles = substituteInFiles;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.SubstituteInFiles);
        }

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(context.CurrentDirectory);
            return this.CompletedTask();
        }
    }
}