using System;
using System.Linq;
using Autofac;
using Calamari.Commands.Support;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register the calamari commands
    /// </summary>
    class CalamariCommandsModule : Module
    {        
        private readonly string name;

        public CalamariCommandsModule(string name)
        {
            this.name = name;
        }

        protected override void Load(ContainerBuilder builder)
        {
            Find()?.Tee(command => builder.RegisterInstance(command).As<ICommand>().SingleInstance());
        }

        /// <summary>
        /// Here we find the one Command class that we want to run
        /// </summary>
        /// <returns>The named command class, or null if none exist</returns>
        protected ICommand Find()
        {
            var fixedName = name.Trim().ToLowerInvariant();
            var found = (from t in ThisAssembly.GetTypes()
            where typeof(ICommand).IsAssignableFrom(t)
            let attribute = (ICommandMetadata) t.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
            where attribute != null
            where attribute.Name == fixedName || attribute.Aliases.Any(a => a == fixedName)
            select t).FirstOrDefault();

            return found == null ? null : (ICommand) Activator.CreateInstance(found);
        }
    }
}
