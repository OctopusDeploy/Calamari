using System.Collections.Generic;
using System.IO.Abstractions;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Threading.Tasks;
using Autofac;
using Calamari.AzureScripting;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;
using Microsoft.Extensions.DependencyInjection;
using Autofac.Extensions.DependencyInjection;
using Bicep.Core;
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
using Bicep.Core.Workspaces;

namespace Calamari.AzureResourceGroup
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);

            builder.RegisterType<TemplateService>();
            builder.RegisterType<ResourceGroupTemplateNormalizer>().As<IResourceGroupTemplateNormalizer>();
            builder.RegisterType<TemplateResolver>().As<ITemplateResolver>().SingleInstance();
            builder.RegisterType<BicepTemplateCompiler>().As<BicepTemplateCompiler>().SingleInstance();

            var services = new ServiceCollection()
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
                .AddSingleton<IBicepAnalyzer, LinterAnalyzer>();
            builder.Populate(services);
        }

        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return typeof(AzureContextScriptWrapper).Assembly;
            yield return typeof(Program).Assembly;
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}