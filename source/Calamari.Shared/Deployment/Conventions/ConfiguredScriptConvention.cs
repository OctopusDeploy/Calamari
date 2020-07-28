using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Deployment.Conventions
{
    public class ConfiguredScriptConvention : IInstallConvention
    {
        readonly ConfiguredScriptBehaviour configuredScriptBehaviour;

        public ConfiguredScriptConvention(ConfiguredScriptBehaviour configuredScriptBehaviour)
        {
            this.configuredScriptBehaviour = configuredScriptBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (configuredScriptBehaviour.IsEnabled(deployment))
            {
                configuredScriptBehaviour.Execute(deployment).Wait();;
            }
        }

        public static string GetScriptName(string deploymentStage, ScriptSyntax scriptSyntax)
        {
            return $"Octopus.Action.CustomScripts.{deploymentStage}.{scriptSyntax.FileExtension()}";
        }
    }
}