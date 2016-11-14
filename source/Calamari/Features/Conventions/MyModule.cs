using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Shared.Convention;

namespace Calamari.Features.Conventions
{
    public class MyModule : IModule
    {
        public void Register(ICalamariContainer calamariContainer)
        {
            calamariContainer.RegisterInstance<Shared.ICalamariFileSystem>(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            calamariContainer.RegisterInstance<Shared.ILog>(new LogWrapper());
            calamariContainer.RegisterInstance(new CombinedScriptEngine());
             //var scriptCapability = new CombinedScriptEngine();

        }
    }

    internal class CalamariCalamariContainer : ICalamariContainer
    {
        internal Dictionary<Type, object> Registrations = new Dictionary<Type, object>();

        public void RegisterInstance<TType>(TType instance)
        {
            var type = typeof(TType);
            if (Registrations.ContainsKey(type))
                throw new Exception($"A registration for type {type.Name} already exists.");
            Registrations.Add(type, instance);
        }
    } 
}
