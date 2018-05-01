using Calamari.Commands;
using Calamari.Commands.Support;
using System;
using System.Linq;
using System.Reflection;
using Autofac;

namespace Calamari.Modules
{
    class CommandLocator
    {
        /// <summary>
        /// Here we find the one Command class that we want to run
        /// </summary>
        /// <returns>The named command class, or null if none exist</returns>
        public Type Find(string name, Assembly assembly)
        {
            var fixedName = name.Trim().ToLowerInvariant();
            var found = (from t in assembly.GetTypes()
                where typeof(ICommand).IsAssignableFrom(t)
                let attribute = (ICommandMetadata)t.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
                where attribute != null
                where attribute.Name == fixedName || attribute.Aliases.Any(a => a == fixedName)
                select t).FirstOrDefault();

            return found;
        }

        /// <summary>
        /// Get all the command attributes
        /// </summary>
        /// <returns>All the Command Attributes in the given assembly</returns>
        public ICommandMetadata[] List(Assembly assembly)
        {
            return
                (from t in assembly.GetTypes()
                    where typeof(ICommand).IsAssignableFrom(t)
                    let attribute = (ICommandMetadata)t.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
                    where attribute != null
                    select attribute).ToArray();
        }

        public ICommand GetOptionalNamedCommand(IComponentContext ctx, string named)
        {
            try
            {
                return ctx.ResolveNamed<ICommand>(named);
            }
            catch
            {
                return null;
            }
        }
    }
}
