#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.FunctionScriptContributions;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common
{
    public abstract class CalamariFlavourProgramAsync
    {
        readonly ILog log;

        protected CalamariFlavourProgramAsync(ILog log)
        {
            this.log = log;
        }

        protected virtual void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
            builder.RegisterType<VariablesFactory>().AsSelf();
            builder.Register(c => c.Resolve<VariablesFactory>().Create(options)).As<IVariables>().SingleInstance();
            builder.RegisterType<ScriptEngine>().As<IScriptEngine>();
            builder.RegisterType<VariableLogger>().AsSelf();
            builder.RegisterInstance(log).As<ILog>().SingleInstance();
            builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>().SingleInstance();
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>().SingleInstance();
            builder.RegisterType<FileSubstituter>().As<IFileSubstituter>();
            builder.RegisterType<SubstituteInFiles>().As<ISubstituteInFiles>();
            builder.RegisterType<CombinedPackageExtractor>().As<ICombinedPackageExtractor>();
            builder.RegisterType<ExtractPackage>().As<IExtractPackage>();
            builder.RegisterType<AssemblyEmbeddedResources>().As<ICalamariEmbeddedResources>();
            builder.RegisterType<ConfigurationVariablesReplacer>().As<IConfigurationVariablesReplacer>();
            builder.RegisterType<TransformFileLocator>().As<ITransformFileLocator>();
            builder.RegisterType<JsonFormatVariableReplacer>().As<IFileFormatVariableReplacer>();
            builder.RegisterType<YamlFormatVariableReplacer>().As<IFileFormatVariableReplacer>();
            builder.RegisterType<StructuredConfigVariablesService>().As<IStructuredConfigVariablesService>();
            builder.Register(context => ConfigurationTransformer.FromVariables(context.Resolve<IVariables>(), context.Resolve<ILog>())).As<IConfigurationTransformer>();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            builder.RegisterType<CodeGenFunctionsRegistry>().SingleInstance();

            var assemblies = GetAllAssembliesToRegister().ToArray();

            builder.RegisterAssemblyTypes(assemblies).AssignableTo<ICodeGenFunctions>().As<ICodeGenFunctions>().SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                   .AssignableTo<IScriptWrapper>()
                   .Except<TerminalScriptWrapper>()
                   .As<IScriptWrapper>()
                   .SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                   .Where(t => t.IsAssignableTo<IBehaviour>() && !t.IsAbstract)
                   .AsSelf()
                   .InstancePerDependency();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<ICommandAsync>()
                .Where(t => t.GetCustomAttribute<CommandAttribute>().Name
                    .Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .Named<ICommandAsync>(t => t.GetCustomAttribute<CommandAttribute>().Name);

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<PipelineCommand>()
                .Where(t => t.GetCustomAttribute<CommandAttribute>().Name
                    .Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .Named<PipelineCommand>(t => t.GetCustomAttribute<CommandAttribute>().Name);
        }

        protected virtual IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return GetType().Assembly;
        }

        protected async Task<int> Run(string[] args)
        {
            try
            {
                SecurityProtocols.EnableAllSecurityProtocols();
                var options = CommonOptions.Parse(args);

                log.Verbose($"Calamari Version: {GetType().Assembly.GetInformationalVersion()}");

                if (options.Command.Equals("version", StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                var envInfo = string.Join($"{Environment.NewLine}  ",
                    EnvironmentHelper.SafelyGetEnvironmentInformation());
                log.Verbose($"Environment Information: {Environment.NewLine}  {envInfo}");

                EnvironmentHelper.SetEnvironmentVariable("OctopusCalamariWorkingDirectory",
                    Environment.CurrentDirectory);
                ProxyInitializer.InitializeDefaultProxy();

                var builder = new ContainerBuilder();
                ConfigureContainer(builder, options);
                using (var container = builder.Build())
                {
                    container.Resolve<VariableLogger>().LogVariables();

                    await ResolveAndExecuteCommand(container, options);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ConsoleLog.Instance, ex);
            }
        }

        IEnumerable<Assembly> GetAllAssembliesToRegister()
        {
            var programAssemblies = GetProgramAssembliesToRegister();

            foreach (var assembly in programAssemblies)
                yield return assembly; // Calamari Flavour & dependencies

            yield return typeof(CalamariFlavourProgramAsync).Assembly; // Calamari.Common
        }

        Task ResolveAndExecuteCommand(ILifetimeScope container, CommonOptions options)
        {
            try
            {
                if (container.IsRegisteredWithName<PipelineCommand>(options.Command))
                {
                    var pipeline = container.ResolveNamed<PipelineCommand>(options.Command);
                    var variables = container.Resolve<IVariables>();
                    return pipeline.Execute(container, variables);
                }

                var command = container.ResolveNamed<ICommandAsync>(options.Command);
                return command.Execute();
            }
            catch (Exception e) when (e is ComponentNotRegisteredException ||
                                      e is DependencyResolutionException)
            {
                throw new CommandException($"Could not find the command {options.Command}");
            }
        }
    }
}
#endif