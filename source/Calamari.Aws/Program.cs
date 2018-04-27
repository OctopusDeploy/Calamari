using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using System.Collections.Generic;
using Autofac;
using Calamari.Aws.Modules;
using Calamari.Modules;
using Calamari.Util;

namespace Calamari.Aws
{
    class Program : Calamari.Program
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();

        public Program(string displayName,
            string informationalVersion,
            string[] environmentInformation,
            ICommand command) : base(displayName, informationalVersion, environmentInformation, command)
        {

        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer(args))
            {
                using (var scope = container.BeginLifetimeScope(
                    builder =>
                    {
                        builder.RegisterModule(new CalamariProgramModule());
                        builder.RegisterModule(new CalamariCommandsModule(PluginUtils.GetFirstArgument(args), typeof(Calamari.Program).Assembly));
                        builder.RegisterModule(new CalamariCommandsModule(PluginUtils.GetFirstArgument(args), typeof(Program).Assembly));
                        builder.RegisterModule(new CalamariPluginsModule());                        
                    }))
                {
                    return scope.Resolve<Program>().Execute(args);
                }
            }
        }
    }
}