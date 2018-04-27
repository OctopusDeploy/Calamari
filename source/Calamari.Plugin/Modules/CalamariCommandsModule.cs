using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Calamari.Commands;
using Calamari.Commands.Support;
using Octopus.CoreUtilities.Extensions;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register the calamari commands
    /// </summary>
    public class CalamariCommandsModule : Module
    {        
        private readonly string name;
        private readonly Assembly assembly;

        public CalamariCommandsModule(string name, Assembly assembly)
        {
            this.name = name;
            this.assembly = assembly;
        }

        public CalamariCommandsModule(string name)
        {
            this.name = name;
            this.assembly = ThisAssembly;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType(Find()).As<ICommand>().SingleInstance();
        }

        /// <summary>
        /// Here we find the one Command class that we want to run
        /// </summary>
        /// <returns>The named command class, or null if none exist</returns>
        protected Type Find()
        {
            var fixedName = name.Trim().ToLowerInvariant();
            var found = (from t in assembly.GetTypes()
            where typeof(ICommand).IsAssignableFrom(t)
            let attribute = (ICommandMetadata) t.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
            where attribute != null
            where attribute.Name == fixedName || attribute.Aliases.Any(a => a == fixedName)
            select t).FirstOrDefault();

            return found ?? typeof(NoOpCommand);
        }
    }
}
