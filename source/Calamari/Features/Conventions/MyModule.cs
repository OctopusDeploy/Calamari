using System;
using System.Collections.Generic;
using Calamari.Extensibility.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;

namespace Calamari.Features.Conventions
{
    public class MyModule : IModule
    {
        public void Register(ICalamariContainer calamariContainer)
        {
            calamariContainer.RegisterInstance<ICalamariFileSystem>(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
      
            calamariContainer.RegisterInstance(new CombinedScriptEngine());
             //var scriptCapability = new CombinedScriptEngine();

        }
    }

    internal class CalamariContainer : ICalamariContainer
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
