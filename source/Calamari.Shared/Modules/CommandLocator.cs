using Calamari.Commands;
using Calamari.Commands.Support;
using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core.Registration;
using Calamari.Util;

namespace Calamari.Modules
{
    public class CommandLocator : ICommandLocator
    {
        public Type Find(string name, Assembly assembly)
        {
            var fixedName = name.Trim().ToLowerInvariant();
            var found = (from t in assembly.GetTypes()
                where typeof(ICommand).IsAssignableFrom(t)
                let attribute = (CommandAttribute)t.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
                where attribute != null
                where attribute.Name == fixedName
                select t).FirstOrDefault();

            return found;
        }

        public ICommand GetOptionalNamedCommand(IComponentContext ctx, string named)
        {
            try
            {
                return ctx.ResolveNamed<ICommand>(named);
            }
            catch (ComponentNotRegisteredException)
            {
                // If there is no component registered with the name we're looking for then let it
                // through to the keeper. Any other exception with
                // registrations should go boom so we can see exactly what's wrong with the registrations.
                return null;
            }
        }
    }
}
