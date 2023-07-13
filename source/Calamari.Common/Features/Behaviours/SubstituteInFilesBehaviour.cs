using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Behaviours
{
    public class SubstituteInFilesBehaviour : IBehaviour
    {
        readonly ISubstituteInFiles substituteInFiles;
        private readonly string subdirectory;

        public SubstituteInFilesBehaviour(
            ISubstituteInFiles substituteInFiles,
            string subdirectory = "")
        {
            this.substituteInFiles = substituteInFiles;
            this.subdirectory = subdirectory;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.IsFeatureEnabled(KnownVariables.Features.SubstituteInFiles);
        }

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(Path.Combine(context.CurrentDirectory, subdirectory));
            return this.CompletedTask();
        }
    }
}