using System.IO;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests;

[TestFixture]
public class BicepTemplateCompilerFixture
{
    [Test]
    public void TestCompile()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAzResourceTypeLoader, AzResourceTypeLoader>()
            .AddSingleton<IFeatureProvider, FeatureProvider>()
            .AddSingleton<INamespaceProvider, DefaultNamespaceProvider>()
            .AddSingleton<IContainerRegistryClientFactory, ContainerRegistryClientFactory>()
            .AddSingleton<ITokenCredentialFactory, TokenCredentialFactory>()
            .AddSingleton<ITemplateSpecRepositoryFactory, TemplateSpecRepositoryFactory>()
            .AddSingleton<IFileResolver, FileResolver>()
            .AddSingleton<IModuleDispatcher, ModuleDispatcher>()
            .AddSingleton<IModuleRegistryProvider, DefaultModuleRegistryProvider>()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IConfigurationManager, ConfigurationManager>()
            .AddSingleton<IApiVersionProvider, ApiVersionProvider>()
            .AddSingleton<IBicepAnalyzer, LinterAnalyzer>()
            .AddSingleton<IBicepTemplateCompiler, BicepTemplateCompiler>()
            .BuildServiceProvider();

        var compiler = services.GetRequiredService<IBicepTemplateCompiler>();
        
        var bicepPath = "./Packages/Bicep/container_app_sample.bicep";
        var expected = File.ReadAllText("./Packages/Bicep/container_app_sample.json");

        var got = compiler.Compile(bicepPath);
        
        Assert.AreEqual(expected, got);
    }
}