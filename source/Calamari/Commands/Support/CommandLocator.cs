using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Util;

namespace Calamari.Commands.Support
{
    public class CommandLocator : ICommandLocator
    {
        private readonly List<Assembly> assemblies = new List<Assembly>(); 

        public static readonly CommandLocator Instance = new CommandLocator();

        public void RegisterAssemblies(params Assembly[] assemblies)
        {
           this.assemblies.AddRange(assemblies); 
        }

        public ICommandMetadata[] List()
        {
            return
                (from t in assemblies.SelectMany(a => a.GetTypes())
                    where typeof (ICommand).IsAssignableFrom(t)
                    let attribute = (ICommandMetadata) t.GetTypeInfo().GetCustomAttributes(typeof (CommandAttribute), true).FirstOrDefault()
                    where attribute != null
                    select attribute).ToArray();
        }

        public ICommand Find(string name)
        {
            name = name.Trim().ToLowerInvariant();
            var found = (from t in assemblies.SelectMany(a => a.GetTypes())
                where typeof (ICommand).IsAssignableFrom(t)
                let attribute = (ICommandMetadata) t.GetTypeInfo().GetCustomAttributes(typeof (CommandAttribute), true).FirstOrDefault()
                where attribute != null
                where attribute.Name == name || attribute.Aliases.Any(a => a == name)
                select t).FirstOrDefault();

            return found == null ? null : (ICommand) Activator.CreateInstance(found);
        }
    }
}