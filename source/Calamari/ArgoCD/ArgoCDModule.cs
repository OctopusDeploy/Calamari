using Autofac;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.ArgoCD.GitHub;

namespace Calamari.ArgoCD
{
    public class ArgoCDModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DeploymentConfigFactory>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<CommitMessageGenerator>().As<ICommitMessageGenerator>().InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(GetType().Assembly)
                   .AssignableTo<IGitVendorPullRequestClientFactory>()
                   .Except<GitVendorPullRequestClientResolver>()
                   .As<IGitVendorPullRequestClientFactory>();

            builder.RegisterType<GitVendorPullRequestClientResolver>().As<IGitVendorAgnosticPullRequestClientFactory>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDManifestsFileMatcher>().As<IArgoCDManifestsFileMatcher>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDFilesUpdatedReporter>().As<IArgoCDFilesUpdatedReporter>().InstancePerLifetimeScope();
        }
    }
}