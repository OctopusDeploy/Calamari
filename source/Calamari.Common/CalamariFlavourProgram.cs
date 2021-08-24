using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Calamari.Common.Commands;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.FunctionScriptContributions;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common
{
    public abstract class CalamariFlavourProgram
    {
        protected readonly ILog Log;

        protected CalamariFlavourProgram(ILog log)
        {
            Log = log;
        }

        protected virtual int Run(string[] args)
        {
            try
            {
                SecurityProtocols.EnableAllSecurityProtocols();
                var options = CommonOptions.Parse(args);

                Log.Verbose($"Calamari Version: {GetType().Assembly.GetInformationalVersion()}");

                if (options.Command.Equals("version", StringComparison.OrdinalIgnoreCase))
                    return 0;

                var envInfo = string.Join($"{Environment.NewLine}  ",
                    EnvironmentHelper.SafelyGetEnvironmentInformation());
                Log.Verbose($"Environment Information: {Environment.NewLine}  {envInfo}");

                EnvironmentHelper.SetEnvironmentVariable("OctopusCalamariWorkingDirectory",
                    Environment.CurrentDirectory);
                ProxyInitializer.InitializeDefaultProxy();

                var builder = new ContainerBuilder();
                ConfigureContainer(builder, options);

                using var container = builder.Build();
                container.Resolve<VariableLogger>().LogVariables();

                #if DEBUG

                var waitForDebugger = container.Resolve<IVariables>().Get(KnownVariables.Calamari.WaitForDebugger);

                if (string.Equals(waitForDebugger, "true", StringComparison.CurrentCultureIgnoreCase))
                {
                    using var proc = Process.GetCurrentProcess();
                    Log.Info($"Waiting for debugger to attach... (PID: {proc.Id})");

                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(100);
                    }
                }

                #endif

                return ResolveAndExecuteCommand(container, options);
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ConsoleLog.Instance, ex);
            }
        }

        protected virtual int ResolveAndExecuteCommand(IContainer container, CommonOptions options)
        {
            try
            {
                var command = container.ResolveNamed<ICommand>(options.Command);
                return command.Execute();
            }
            catch (Exception e) when (e is ComponentNotRegisteredException ||
                e is DependencyResolutionException)
            {
                throw new CommandException($"Could not find the command {options.Command}");
            }
        }

        protected virtual void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
            builder.RegisterType<VariablesFactory>().AsSelf();
            builder.Register(c => c.Resolve<VariablesFactory>().Create(options)).As<IVariables>().SingleInstance();
            builder.RegisterType<ScriptEngine>().As<IScriptEngine>();
            builder.RegisterType<VariableLogger>().AsSelf();
            builder.RegisterInstance(Log).As<ILog>().SingleInstance();
            builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>().SingleInstance();
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>().SingleInstance();
            builder.RegisterType<FileSubstituter>().As<IFileSubstituter>();
            builder.RegisterType<SubstituteInFiles>().As<ISubstituteInFiles>();
            builder.RegisterType<CombinedPackageExtractor>().As<ICombinedPackageExtractor>();
            builder.RegisterType<ExtractPackage>().As<IExtractPackage>();
            builder.RegisterType<CodeGenFunctionsRegistry>().SingleInstance();
            builder.RegisterType<AssemblyEmbeddedResources>().As<ICalamariEmbeddedResources>();

            var assemblies = GetAllAssembliesToRegister().ToArray();

            builder.RegisterAssemblyTypes(assemblies).AssignableTo<ICodeGenFunctions>().As<ICodeGenFunctions>().SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IScriptWrapper>()
                .Except<TerminalScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<ICommand>()
                .Where(t => ((CommandAttribute)Attribute.GetCustomAttribute(t, typeof(CommandAttribute))).Name
                    .Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .Named<ICommand>(t => ((CommandAttribute)Attribute.GetCustomAttribute(t, typeof(CommandAttribute))).Name);

            builder.RegisterInstance(options).AsSelf().SingleInstance();
        }

        protected virtual Assembly GetProgramAssemblyToRegister()
        {
            return GetType().Assembly;
        }

        protected virtual IEnumerable<Assembly> GetAllAssembliesToRegister()
        {
            var programAssembly = GetProgramAssemblyToRegister();
            if (programAssembly != null)
                yield return programAssembly; // Calamari Flavour

            yield return typeof(CalamariFlavourProgram).Assembly; // Calamari.Common
        }
    }
}