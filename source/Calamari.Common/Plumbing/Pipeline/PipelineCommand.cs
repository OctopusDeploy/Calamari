using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Pipeline
{
    public abstract class PipelineCommand
    {
        protected virtual IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            return Enumerable.Empty<IBeforePackageExtractionBehaviour>();
        }

        protected virtual IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            return Enumerable.Empty<IAfterPackageExtractionBehaviour>();
        }

        protected virtual IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            return Enumerable.Empty<IPreDeployBehaviour>();
        }

        protected abstract IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver);

        protected virtual IEnumerable<IPostDeployBehaviour> PostDeploy(PostDeployResolver resolver)
        {
            return Enumerable.Empty<IPostDeployBehaviour>();
        }

        public async Task Execute(ILifetimeScope lifetimeScope, IVariables variables)
        {
            var pathToPrimaryPackage = variables.GetPathToPrimaryPackage(lifetimeScope.Resolve<ICalamariFileSystem>(), false);
            var deployment = new RunningDeployment(pathToPrimaryPackage, variables);

            foreach (var behaviour in BeforePackageExtraction(new BeforePackageExtractionResolver(lifetimeScope)))
            {
                await ExecuteBehaviour(deployment, behaviour);
            }

            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ExtractBehaviour>());

            foreach (var behaviour in AfterPackageExtraction(new AfterPackageExtractionResolver(lifetimeScope)))
            {
                await ExecuteBehaviour(deployment, behaviour);
            }

            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("deploymentStage", DeploymentStages.PreDeploy)));
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<PackagedScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.PreDeploy)));

            foreach (var behaviour in PreDeploy(new PreDeployResolver(lifetimeScope)))
            {
                await ExecuteBehaviour(deployment, behaviour);
            }

            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<SubstituteInFilesBehaviour>());
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationTransformsBehaviour>());
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationVariablesBehaviour>());
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<JsonConfigurationVariablesBehaviour>());
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<PackagedScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.Deploy)));
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("deploymentStage", DeploymentStages.Deploy)));

            foreach (var behaviour in Deploy(new DeployResolver(lifetimeScope)))
            {
                await ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in PostDeploy(new PostDeployResolver(lifetimeScope)))
            {
                await ExecuteBehaviour(deployment, behaviour);
            }

            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<PackagedScriptBehaviour>(new NamedParameter("scriptFilePrefix", DeploymentStages.PostDeploy)));
            await ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfiguredScriptBehaviour>(new NamedParameter("deploymentStage", DeploymentStages.PostDeploy)));
        }

        static async Task ExecuteBehaviour(RunningDeployment context, IBehaviour behaviour)
        {
            if (behaviour.IsEnabled(context))
            {
                await behaviour.Execute(context);
            }
        }
    }
}