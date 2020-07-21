using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    public class ConfigurationTransformsConvention : IInstallConvention
    {
        readonly ConfigurationTransformsBehaviour configurationTransformsBehaviour;

        public ConfigurationTransformsConvention(ConfigurationTransformsBehaviour configurationTransformsBehaviour)
        {
            this.configurationTransformsBehaviour = configurationTransformsBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (configurationTransformsBehaviour.IsEnabled(deployment))
            {
                configurationTransformsBehaviour.Execute(deployment).Wait();
            }
        }
    }
}
