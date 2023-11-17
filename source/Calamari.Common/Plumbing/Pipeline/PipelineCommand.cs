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
            return Enumerable.Empty<IPreDeployBehaviour>();
        }

        protected abstract IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver);

        protected virtual IEnumerable<IPostDeployBehaviour> PostDeploy(PostDeployResolver resolver)
        {
            return Enumerable.Empty<IPostDeployBehaviour>();
        }

        protected virtual IEnumerable<IOnFinishBehaviour> OnFinish(OnFinishResolver resolver)
        {
            return Enumerable.Empty<IOnFinishBehaviour>();
        }

        protected virtual bool IncludePackagedScriptBehaviour { get; } = true;
        protected virtual bool IncludeConfiguredScriptBehaviour { get; } = true;

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

            foreach (var scriptBehaviour in MaybeIncludeScriptBehaviours<PreDeployPackagedScriptBehaviour, PreDeployConfiguredScriptBehaviour>(lifetimeScope))
            {
                yield return ExecuteBehaviour(deployment, scriptBehaviour);
            }

            foreach (var behaviour in PreDeploy(new PreDeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<SubstituteInFilesBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationTransformsBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<ConfigurationVariablesBehaviour>());
            yield return ExecuteBehaviour(deployment, lifetimeScope.Resolve<StructuredConfigurationVariablesBehaviour>());

            foreach (var scriptBehaviour in MaybeIncludeScriptBehaviours<DeployPackagedScriptBehaviour, DeployConfiguredScriptBehaviour>(lifetimeScope))
            {
                yield return ExecuteBehaviour(deployment, scriptBehaviour);
            }

            foreach (var behaviour in Deploy(new DeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var behaviour in PostDeploy(new PostDeployResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }

            foreach (var scriptBehaviour in MaybeIncludeScriptBehaviours<PostDeployPackagedScriptBehaviour, PostDeployConfiguredScriptBehaviour>(lifetimeScope))
            {
                yield return ExecuteBehaviour(deployment, scriptBehaviour);
            }
            foreach (var behaviour in OnFinish(new OnFinishResolver(lifetimeScope)))
            {
                yield return ExecuteBehaviour(deployment, behaviour);
            }
        }

        IEnumerable<IBehaviour> MaybeIncludeScriptBehaviours<TPackagedScriptBehaviour, TConfiguredScriptBehaviour>(ILifetimeScope lifetimeScope)
            where TPackagedScriptBehaviour : PackagedScriptBehaviour
            where TConfiguredScriptBehaviour : ConfiguredScriptBehaviour
        {
            if (IncludePackagedScriptBehaviour)
            {
                yield return lifetimeScope.Resolve<TPackagedScriptBehaviour>();
            }

            if (IncludeConfiguredScriptBehaviour)
            {
                yield return lifetimeScope.Resolve<TConfiguredScriptBehaviour>();
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
