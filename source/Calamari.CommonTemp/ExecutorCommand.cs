using System.Threading.Tasks;
using Autofac;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;

namespace Calamari.CommonTemp
{
    public class ExecutorCommand : ICommandAsync
    {
        readonly ILifetimeScope lifetimeScope;
        readonly CommandPipelineRegistration pipelineRegistration;
        readonly IVariables variables;

        public ExecutorCommand(ILifetimeScope lifetimeScope, CommandPipelineRegistration pipelineRegistration,
            IVariables variables)
        {
            this.lifetimeScope = lifetimeScope;
            this.pipelineRegistration = pipelineRegistration;
            this.variables = variables;
        }

        public async Task Execute()
        {
            var pathToPrimaryPackage = variables.GetPathToPrimaryPackage(lifetimeScope.Resolve<ICalamariFileSystem>(), false);
            var deployment = new RunningDeployment(pathToPrimaryPackage, variables);

            foreach (var behaviour in pipelineRegistration.BeforePackageExtraction(new BeforePackageExtractionResolver(lifetimeScope)))
            {
                await behaviour.Execute(deployment);
            }

            await lifetimeScope.Resolve<ExtractBehaviour>().Execute(deployment);

            foreach (var behaviour in pipelineRegistration.AfterPackageExtraction(new AfterPackageExtractionResolver(lifetimeScope)))
            {
                await behaviour.Execute(deployment);
            }

            await lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.PreDeploy)).Execute(deployment);
            await lifetimeScope.Resolve<PackagedScriptConvention>(new NamedParameter("scriptFilePrefix", DeploymentStages.PreDeploy)).Execute(deployment);

            foreach (var behaviour in pipelineRegistration.PreDeploy(new PreDeployResolver(lifetimeScope)))
            {
                await behaviour.Execute(deployment);
            }

            await lifetimeScope.Resolve<SubstituteInFilesBehaviour>().Execute(deployment);
            await lifetimeScope.Resolve<ConfigurationTransformsBehaviour>().Execute(deployment);
            await lifetimeScope.Resolve<ConfigurationVariablesBehaviour>().Execute(deployment);
            await lifetimeScope.Resolve<JsonConfigurationVariablesBehaviour>().Execute(deployment);
            await lifetimeScope.Resolve<PackagedScriptConvention>(new NamedParameter("scriptFilePrefix", DeploymentStages.Deploy)).Execute(deployment);
            await lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.Deploy)).Execute(deployment);

            foreach (var behaviour in pipelineRegistration.Deploy(new DeployResolver(lifetimeScope)))
            {
                await behaviour.Execute(deployment);
            }

            foreach (var behaviour in pipelineRegistration.PostDeploy(new PostDeployResolver(lifetimeScope)))
            {
                await behaviour.Execute(deployment);
            }

            await lifetimeScope.Resolve<PackagedScriptConvention>(new NamedParameter("scriptFilePrefix", DeploymentStages.PostDeploy)).Execute(deployment);
            await lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.PostDeploy)).Execute(deployment);
        }
    }
}