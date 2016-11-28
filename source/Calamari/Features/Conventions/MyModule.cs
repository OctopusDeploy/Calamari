using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Deployment.Conventions;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

namespace Calamari.Features.Conventions
{
    public class MyModule : IModule
    {
        public void Register(ICalamariContainer container)
        {
            var variables = container.Resolve<CalamariVariableDictionary>();

            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var log = new LogWrapper();
            
            container.RegisterInstance<ICalamariFileSystem>(filesystem);
            container.RegisterInstance<ISemaphoreFactory>(SemaphoreFactory.Get());
            container.RegisterInstance<IPackageExtractor>(new PackageExtractor(filesystem, variables, SemaphoreFactory.Get()));
            container.RegisterInstance<IScriptExecution>(new ScriptExecution(filesystem, new CombinedScriptEngine(), commandLineRunner, variables));
            container.RegisterInstance<IFileSubstitution>(new FileSubstitution(filesystem, new FileSubstituter(filesystem), variables, log));
            container.RegisterInstance<Calamari.Extensibility.ILog>(log);
        }
    }

    internal class CalamariContainer : ICalamariContainer
    {
        internal Dictionary<Type, object> Registrations = new Dictionary<Type, object>();


        public void RegisterModule(Type type)
        {
            if (type == null)
                return;

            (Activator.CreateInstance(type) as IModule)?.Register(this);
        }

        public void RegisterModule(IModule type)
        {
            type.Register(this);
        }

        public void RegisterInstance<TType>(TType instance)
        {
            var type = typeof(TType);
            if (Registrations.ContainsKey(type))
                throw new Exception($"A registration for type {type.Name} already exists.");
            Registrations.Add(type, instance);
        }

        public TType Resolve<TType>()
        {
            var type = typeof(TType);
            if (!Registrations.ContainsKey(type))
            {
                throw new Exception($"Unable to find class registered for type {type.Name}.");
            }
            return (TType)Registrations[type];

        }
    }

   
}
