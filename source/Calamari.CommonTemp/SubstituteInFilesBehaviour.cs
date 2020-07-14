using System.Threading.Tasks;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.CommonTemp
{
    internal class SubstituteInFilesBehaviour : IBehaviour
    {
        readonly ISubstituteInFiles substituteInFiles;

        public SubstituteInFilesBehaviour(ISubstituteInFiles substituteInFiles)
        {
            this.substituteInFiles = substituteInFiles;
        }

        public Task Execute(RunningDeployment context)
        {
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(context);
            return this.CompletedTask();
        }
    }
}