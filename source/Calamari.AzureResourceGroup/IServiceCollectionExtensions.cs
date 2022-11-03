using System.IO.Abstractions;
using Bicep.Core.Analyzers.Interfaces;
using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Analyzers.Linter.ApiVersions;
using Bicep.Core.Configuration;
using Bicep.Core.Features;
using Bicep.Core.FileSystem;
using Bicep.Core.Registry;
using Bicep.Core.Registry.Auth;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.TypeSystem.Az;
using Microsoft.Extensions.DependencyInjection;

namespace Calamari.AzureResourceGroup
{
    public static class ServiceCollectionExtensions
    {
        // TODO: this method should be available in the next release of Bicep.Core
        // at the moment, there is a `AddBicepCore` method that registers the required dependencies
        // that is available on master. It hasn't been released to NuGet however :)
        public static IServiceCollection AddBicep(this IServiceCollection services) 
            => services
                .AddSingleton<IAzResourceTypeLoader, AzResourceTypeLoader>()
                .AddSingleton<IFeatureProvider, FeatureProvider>()
                .AddSingleton<INamespaceProvider, DefaultNamespaceProvider>()
                .AddSingleton<ITemplateSpecRepositoryFactory, TemplateSpecRepositoryFactory>()
                .AddSingleton<IContainerRegistryClientFactory, ContainerRegistryClientFactory>()
                .AddSingleton<ITokenCredentialFactory, TokenCredentialFactory>()
                .AddSingleton<IFileResolver, FileResolver>()
                .AddSingleton<IModuleDispatcher, ModuleDispatcher>()
                .AddSingleton<IModuleRegistryProvider, DefaultModuleRegistryProvider>()
                .AddSingleton<IFileSystem, FileSystem>()
                .AddSingleton<IConfigurationManager, ConfigurationManager>()
                .AddSingleton<IApiVersionProvider, ApiVersionProvider>()
                .AddSingleton<IBicepAnalyzer, LinterAnalyzer>()
                .AddSingleton<IBicepTemplateCompiler, BicepTemplateCompiler>();
    }
}