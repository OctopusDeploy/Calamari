using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Calamari.Commands;
using Calamari.Deployment;
using Calamari.Deployment.Journal;
using Calamari.HealthChecks;
using Calamari.Hooks;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;
using Calamari.Variables;
using Calamari.Plumbing;
using NuGet;

namespace Calamari
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                SecurityProtocols.EnableAllSecurityProtocols();

                var options = CommonOptions.Parse(args);
                return new Program().Run(options);
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        internal int Run(CommonOptions options)
        {
            Log.Verbose($"Calamari Version: {typeof(Program).Assembly.GetInformationalVersion()}");

            if (options.Command.Equals("version", StringComparison.OrdinalIgnoreCase))
                return 0;

            var envInfo = string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation());
            Log.Verbose($"Environment Information: {Environment.NewLine}  {envInfo}");

            EnvironmentHelper.SetEnvironmentVariable(SpecialVariables.CalamariWorkingDirectory, Environment.CurrentDirectory);
            ProxyInitializer.InitializeDefaultProxy();

            using (var container = BuildContainer(options).Build())
            {
                container.Resolve<VariableLogger>().LogVariables();

                var command = container.Resolve<ICommand[]>();
                if (command.Length == 0)
                    throw new CommandException($"Could not find the command {options.Command}");
                if (command.Length > 1)
                    throw new CommandException($"Multiple commands found with the name {options.Command}");

                return command[0].Execute(options.RemainingArguments.ToArray());
            }
        }

        protected virtual ContainerBuilder BuildContainer(CommonOptions options)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
            builder.RegisterType<VariablesFactory>().AsSelf();
            builder.Register(c => c.Resolve<VariablesFactory>().Create(options)).As<IVariables>().SingleInstance();
            builder.RegisterType<CombinedScriptEngine>().AsSelf().As<IScriptEngine>();
            builder.RegisterType<VariableLogger>().AsSelf();
            builder.RegisterType<LogWrapper>().As<ILog>().SingleInstance();
            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().SingleInstance();
            builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>().SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            
            
            var assemblies = GetAllAssembliesToRegister(options).ToArray();
                
            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IScriptWrapper>()
                .Except<TerminalScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();
            
            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IDoesDeploymentTargetTypeHealthChecks>()
                .As<IDoesDeploymentTargetTypeHealthChecks>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<ICommand>()
                .Where(t => t.GetCustomAttribute<CommandAttribute>().Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .As<ICommand>();

            return builder;
        }

        static IEnumerable<Assembly> GetAllAssembliesToRegister(CommonOptions options)
        {
            yield return typeof(Program).Assembly; // Calamari
            yield return typeof(ApplyDeltaCommand).Assembly; // Calamari.Shared
            foreach (var extension in options.Extensions)
                yield return Assembly.Load(extension) ?? throw new CommandException($"Could not find the extension {extension}");
        }
        
    }
}