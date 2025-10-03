using System;
using System.IO;
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
        readonly string subdirectory;
        readonly ISubstituteFileMatcher? customFileMatcher;

        public SubstituteInFilesBehaviour(
            ISubstituteInFiles substituteInFiles,
            string subdirectory = "",
            ISubstituteFileMatcher? customFileMatcher = null)
        {
            this.substituteInFiles = substituteInFiles;
            this.subdirectory = subdirectory;
            this.customFileMatcher = customFileMatcher;
        }

        public bool IsEnabled(RunningDeployment context) => context.Variables.IsFeatureEnabled(KnownVariables.Features.SubstituteInFiles);

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(Path.Combine(context.CurrentDirectory, subdirectory), customFileMatcher: customFileMatcher);
            return Task.CompletedTask;
        }
    }
}