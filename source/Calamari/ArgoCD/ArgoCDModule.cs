using Autofac;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.GitHub;
using LibGit2Sharp;

namespace Calamari.ArgoCD
{
    public class ArgoCDModule : Module
    {
        static ArgoCDModule()
        {
            // Note this cannot be set in the RepositoryFactory as it causes tests to fail, due to the following issue.
            
            // LibGit2Sharp custom sub-transports are registered by calling a static registration
            // method on GlobalSettings. Additionally, if you try and register a multiple transports
            // with the same scheme, it throws an exception. It's not ideal, but it's what we've got
            // to work with.
            //
            // Using the type constructor to make sure that these methods are only called once.
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("http");
            GlobalSettings.RegisterSmartSubtransport<GitHttpSmartSubTransport>("https");
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DeploymentConfigFactory>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<CommitMessageGenerator>().As<ICommitMessageGenerator>().InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(GetType().Assembly)
                   .AssignableTo<IGitVendorApiAdapterFactory>()
                   .Except<GitVendorAgnosticApiAdapterFactory>()
                   .As<IGitVendorApiAdapterFactory>();

            builder.RegisterType<GitVendorAgnosticApiAdapterFactory>().As<IGitVendorAgnosticApiAdapterFactory>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDManifestsFileMatcher>().As<IArgoCDManifestsFileMatcher>().InstancePerLifetimeScope();
            builder.RegisterType<ArgoCDFilesUpdatedReporter>().As<IArgoCDFilesUpdatedReporter>().InstancePerLifetimeScope();
        }
        }
    }
