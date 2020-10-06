#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
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

        protected virtual IEnumerable<IPackageExtractionBehaviour> PackageExtraction(PackageExtractionResolver resolver)
        {
            yield return resolver.Create<ExtractBehaviour>();
        }

        protected virtual IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            return Enumerable.Empty<IAfterPackageExtractionBehaviour>();
        }

        protected virtual IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            yield return resolver.Create<PreDeployConfiguredScriptBehaviour>();
            yield return resolver.Create<PreDeployPackagedScriptBehaviour>();
        }

        protected virtual IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<DeployPackagedScriptBehaviour>();
            yield return resolver.Create<DeployConfiguredScriptBehaviour>();
        }

        protected virtual IEnumerable<IPostDeployBehaviour> PostDeploy(PostDeployResolver resolver)
        {
            yield return resolver.Create<PostDeployPackagedScriptBehaviour>();
            yield return resolver.Create<PostDeployConfiguredScriptBehaviour>();
        }

        protected virtual IEnumerable<IOnFinishBehaviour> OnFinish(OnFinishResolver resolver)
        {
            return Enumerable.Empty<IOnFinishBehaviour>();
        }

        public async Task Execute(ILifetimeScope lifetimeScope, IVariables variables)
        {
            var pathToPrimaryPackage = variables.GetPathToPrimaryPackage(lifetimeScope.Resolve<ICalamariFileSystem>(), false);
            var deployment = new RunningDeployment(pathToPrimaryPackage, variables);

            try
            {
                foreach (var behaviour in GetBehaviours(lifetimeScope, deployment))
                {
                    await behaviour;

                    if (deployment.Variables.GetFlag(KnownVariables.Action.SkipRemainingConventions))
                    {
                        break;
                    }
                }
            }
            catch (Exception installException)
            {
                Console.Error.WriteLine("Running rollback behaviours...");

                deployment.Error(installException);

                try
                {
                    // Rollback behaviours include tasks like DeployFailed.ps1
                    await ExecuteBehaviour(deployment, lifetimeScope.Resolve<RollbackScriptBehaviour>());
                }
                catch (Exception rollbackException)
                {
                    Console.Error.WriteLine(rollbackException);
                }

                throw;
            }
        }

        IEnumerable<Task> GetBehaviours(ILifetimeScope lifetimeScope, RunningDeployment deployment)
        {
            foreach (var behaviour in BeforePackageExtraction(new BeforePackageExtractionResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in PackageExtraction(new PackageExtractionResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in AfterPackageExtraction(new AfterPackageExtractionResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in PreDeploy(new PreDeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<SubstituteInFilesBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationTransformsBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationVariablesBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<StructuredConfigurationVariablesBehaviour>());

            foreach (var behaviour in Deploy(new DeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in PostDeploy(new PostDeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in OnFinish(new OnFinishResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }
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
#endif