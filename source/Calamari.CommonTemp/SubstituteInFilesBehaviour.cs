using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.CommonTemp
{
    internal class SubstituteInFilesBehaviour : IBehaviour
    {
        readonly ISubstituteInFiles substituteInFiles;

        public SubstituteInFilesBehaviour(ISubstituteInFiles substituteInFiles)
        {
            this.substituteInFiles = substituteInFiles;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.GetFlag(PackageVariables.SubstituteInFilesEnabled);
        }

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(context);
            return this.CompletedTask();
        }
    }
}