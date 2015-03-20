using System;
using System.Linq;

namespace Calamari.Commands.Support
{
    public class CommandLocator : ICommandLocator
    {
        public static readonly CommandLocator Instance = new CommandLocator();

        public ICommandMetadata[] List()
        {
            return
                (from t in typeof (CommandLocator).Assembly.GetTypes()
                    where typeof (ICommand).IsAssignableFrom(t)
                    let attribute = (ICommandMetadata) t.GetCustomAttributes(typeof (CommandAttribute), true).FirstOrDefault()
                    where attribute != null
                    select attribute).ToArray();
        }

        public ICommand Find(string name)
        {
            name = name.Trim().ToLowerInvariant();
            var found = (from t in typeof (CommandLocator).Assembly.GetTypes()
                where typeof (ICommand).IsAssignableFrom(t)
                let attribute = (ICommandMetadata) t.GetCustomAttributes(typeof (CommandAttribute), true).FirstOrDefault()
                where attribute != null
                where attribute.Name == name || attribute.Aliases.Any(a => a == name)
                select t).FirstOrDefault();

            return found == null ? null : (ICommand) Activator.CreateInstance(found);
        }
    }
}