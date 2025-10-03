using Autofac;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;

namespace Calamari.ArgoCD
{
    public class ArgoCDModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
#if NET
            builder.RegisterType<DeploymentConfigFactory>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<CommitMessageGenerator>().As<ICommitMessageGenerator>().InstancePerLifetimeScope();
            builder.RegisterType<GitHubClientFactory>().As<IGitHubClientFactory>().InstancePerLifetimeScope();
            builder.RegisterType<GitHubPullRequestCreator>().As<IGitHubPullRequestCreator>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDManifestsFileMatcher>().As<IArgoCDManifestsFileMatcher>().InstancePerLifetimeScope();
#endif
        }
    }
}