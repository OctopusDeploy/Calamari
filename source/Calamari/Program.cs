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
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Substitutions;
using Calamari.Util.Environments;
using Calamari.Variables;
using Calamari.Plumbing;
using NuGet;

namespace Calamari
{
    public class Program : CalamariFlavourProgram
    {
        List<string> extensions;

        protected Program(ILog log) : base(log)
        {
        }
        
        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }

        protected override int ResolveAndExecuteCommand(IContainer container, CommonOptions options)
        {
            var command = container.Resolve<ICommandWithArgs[]>();
            if (command.Length == 0)
                throw new CommandException($"Could not find the command {options.Command}");
            if (command.Length > 1)
                throw new CommandException($"Multiple commands found with the name {options.Command}");

            return command[0].Execute(options.RemainingArguments.ToArray());
        }

        protected override ContainerBuilder BuildContainer(CommonOptions options)
        {
            // Setting extensions here as in the new Modularity world we don't register extensions
            // and GetAllAssembliesToRegister doesn't get passed CommonOptions 
            extensions = options.Extensions;
            
            var builder = base.BuildContainer(options);
            
            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            builder.RegisterType<PackageStore>().As<IPackageStore>().SingleInstance();

            var assemblies = GetAllAssembliesToRegister().ToArray();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IDoesDeploymentTargetTypeHealthChecks>()
                .As<IDoesDeploymentTargetTypeHealthChecks>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<ICommandWithArgs>()
                .Where(t => t.GetCustomAttribute<CommandAttribute>().Name
                    .Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .As<ICommandWithArgs>();
            
            return builder;
        }

        protected override IEnumerable<Assembly> GetAllAssembliesToRegister()
        {
            var assemblies = base.GetAllAssembliesToRegister();
            foreach (var assembly in assemblies)
            {
                yield return assembly;
            }
            yield return typeof(ApplyDeltaCommand).Assembly; // Calamari.Shared
            foreach (var extension in extensions)
                yield return Assembly.Load(extension) ?? throw new CommandException($"Could not find the extension {extension}");
        }
    }
}