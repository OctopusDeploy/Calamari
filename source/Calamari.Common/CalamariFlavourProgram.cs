using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
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
using Calamari.Common.Features.Processes.ScriptIsolation;
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
using Calamari.Common.Util;

namespace Calamari.Common;

public abstract class CalamariFlavourProgram(ILog log)
{
    protected async Task<int> Run(string[] args)
    {
        try
        {
            AppDomainConfiguration.SetDefaultRegexMatchTimeout();

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

            using var container = builder.Build();
            container.Resolve<VariableLogger>().LogVariables();
#if DEBUG
            if (CalamariEnvironment.ShouldWaitForDebugger(container.Resolve<IVariables>()))
            {
                using var proc = Process.GetCurrentProcess();
                log.Info($"Waiting for debugger to attach... (PID: {proc.Id})");

                while (!Debugger.IsAttached)
                {
                    await Task.Delay(1000);
                }
            }
#endif
            var isolation = container.Resolve<IScriptIsolationEnforcer>();
            await using var _ = await isolation.EnforceAsync(options.ScriptIsolation, CancellationToken.None);
            return await ResolveAndExecuteCommand(container, options);
        }
        catch (Exception ex)
        {
            return ConsoleFormatter.PrintError(log, ex);
        }
    }

    async Task<int> ResolveAndExecuteCommand(IContainer container, CommonOptions options)
    {
        try
        {
            if (container.IsRegisteredWithName<PipelineCommand>(options.Command))
            {
                try
                {
                    var pipeline = container.ResolveNamed<PipelineCommand>(options.Command);
                    var variables = container.Resolve<IVariables>();
                    await pipeline.Execute(container, variables);
                    return 0;
                }
                catch (Exception ex)
                {
                    return ConsoleFormatter.PrintError(log, ex);
                }
            }

            return await ResolveAndExecuteCommandWithArgs(container, options);
        }
        catch (Exception e) when (e is ComponentNotRegisteredException or DependencyResolutionException)
        {
            throw new CommandException($"Could not find the command {options.Command}");
        }
    }

    protected abstract Task<int> ResolveAndExecuteCommandWithArgs(IContainer container, CommonOptions options);

    protected virtual void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
    {
        //register the options into the DI
        builder.RegisterInstance(options).AsSelf();

        var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
        builder.RegisterType<ScriptEngine>().As<IScriptEngine>();
        builder.RegisterType<VariableLogger>().AsSelf();
        builder.RegisterInstance(log).As<ILog>().SingleInstance();
        builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>().SingleInstance();
        builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>().SingleInstance();
        builder.RegisterType<CombinedPackageExtractor>().As<ICombinedPackageExtractor>();
        builder.RegisterType<ExtractPackage>().As<IExtractPackage>();
        builder.RegisterType<CodeGenFunctionsRegistry>().SingleInstance();
        builder.RegisterType<AssemblyEmbeddedResources>().As<ICalamariEmbeddedResources>();

        // For Pipeline Commands
        builder.RegisterType<TransformFileLocator>().As<ITransformFileLocator>();
        builder.Register(context => ConfigurationTransformer.FromVariables(context.Resolve<IVariables>(), context.Resolve<ILog>())).As<IConfigurationTransformer>();
        builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
        builder.RegisterType<ConfigurationVariablesReplacer>().As<IConfigurationVariablesReplacer>();

        builder.RegisterModule<VariablesModule>();
        builder.RegisterModule<SubstitutionsModule>();
        builder.RegisterModule<ScriptIsolationModule>();

        var assemblies = GetAllAssembliesToRegister().ToArray();

        builder.RegisterAssemblyTypes(assemblies).AssignableTo<ICodeGenFunctions>().As<ICodeGenFunctions>().SingleInstance();

        builder.RegisterAssemblyTypes(assemblies)
               .AssignableTo<IScriptWrapper>()
               .Except<TerminalScriptWrapper>()
               .As<IScriptWrapper>()
               .SingleInstance();

        // Register Behaviors
        builder.RegisterAssemblyTypes(assemblies)
               .Where(t => t.IsAssignableTo<IBehaviour>() && !t.IsAbstract)
               .AsSelf()
               .InstancePerDependency();

        // Register Pipeline commands
        builder.RegisterAssemblyTypes(assemblies)
               .AssignableTo<PipelineCommand>()
               .WithMetadataFrom<CommandAttribute>()
               .Where(t => t.GetCustomAttribute<CommandAttribute>() is not null)
               .Named<PipelineCommand>(t => t.GetCommandNameFromAttribute());

        builder.RegisterModule<StructuredConfigVariablesModule>();
    }

    protected virtual IEnumerable<Assembly> GetProgramAssembliesToRegister()
    {
        yield return GetType().Assembly;
    }

    protected virtual IEnumerable<Assembly> GetAllAssembliesToRegister()
    {
        var programAssemblies = GetProgramAssembliesToRegister();

        foreach (var assembly in programAssemblies)
            yield return assembly; // Calamari Flavour & dependencies

        yield return typeof(CalamariFlavourProgram).Assembly; // Calamari.Common
    }
}