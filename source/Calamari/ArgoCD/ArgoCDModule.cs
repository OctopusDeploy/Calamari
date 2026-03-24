using Autofac;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab;
using Calamari.ArgoCD.GitHub;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
                   .As<IGitVendorPullRequestClientFactory>()
                   .InstancePerLifetimeScope();

            builder.RegisterType<GitVendorPullRequestClientResolver>().As<IGitVendorPullRequestClientResolver>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDManifestsFileMatcher>().As<IArgoCDManifestsFileMatcher>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDFilesUpdatedReporter>().As<IArgoCDFilesUpdatedReporter>().InstancePerLifetimeScope();
            
            builder.RegisterType<SelfHostedGitLabInspector>().AsSelf().InstancePerLifetimeScope();
            
            builder.RegisterGeneric(typeof(OptionsManager<>))
                   .As(typeof(IOptions<>))
                   .InstancePerLifetimeScope();
            builder.RegisterGeneric(typeof(OptionsManager<>))
                   .As(typeof(IOptionsSnapshot<>))
                   .InstancePerLifetimeScope();
            builder.RegisterGeneric(typeof(OptionsMonitor<>))
                   .As(typeof(IOptionsMonitor<>))
                   .InstancePerLifetimeScope();
            builder.RegisterGeneric(typeof(OptionsFactory<>))
                   .As(typeof(IOptionsFactory<>));
            builder.RegisterGeneric(typeof(OptionsCache<>))
                   .As(typeof(IOptionsMonitorCache<>))
                   .InstancePerLifetimeScope();
            builder.RegisterType<MemoryCache>().As<IMemoryCache>().InstancePerLifetimeScope();
        }
    }
}
