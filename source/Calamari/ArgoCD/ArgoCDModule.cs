using Autofac;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.Git.GitVendorApiAdapters.GitLab;
using Microsoft.Extensions.Caching.Memory;

namespace Calamari.ArgoCD
{
    public class ArgoCDModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DeploymentConfigFactory>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<CommitMessageGenerator>().As<ICommitMessageGenerator>().InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(GetType().Assembly)
                   .AssignableTo<IResolvingGitVendorApiAdapterFactory>()
                   .As<IResolvingGitVendorApiAdapterFactory>();

            builder.RegisterType<GitVendorAgnosticApiAdapterFactory>().As<IGitVendorAgnosticApiAdapterFactory>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDManifestsFileMatcher>().As<IArgoCDManifestsFileMatcher>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDFilesUpdatedReporter>().As<IArgoCDFilesUpdatedReporter>().InstancePerLifetimeScope();
            
            builder.RegisterType<SelfHostedGitLabInspector>().AsSelf().InstancePerLifetimeScope();
        }
    }
}