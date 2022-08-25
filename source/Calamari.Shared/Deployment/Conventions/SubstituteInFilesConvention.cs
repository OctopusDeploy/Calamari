using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    public class SubstituteInFilesConvention : IInstallConvention
    {
        readonly SubstituteInFilesBehaviour substituteInFilesBehaviour;

        public SubstituteInFilesConvention(SubstituteInFilesBehaviour substituteInFilesBehaviour)
        {
            this.substituteInFilesBehaviour = substituteInFilesBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (substituteInFilesBehaviour.IsEnabled(deployment))
            {
                substituteInFilesBehaviour.Execute(deployment).Wait();
            }
        }
    }
}