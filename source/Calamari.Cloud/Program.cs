using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Cloud
{
    class Program : Calamari.Program
    {
        public Program() : base("Calamari.Cloud", typeof(Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation())
        {
 
        }

        static int Main(string[] args)
        {
            var program = new Program();
            return program.Execute(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            new[]
                {
                    typeof(Calamari.Program).Assembly,
                    typeof(Aws.AssemblyMarker).Assembly,
                    typeof(Azure.AssemblyMarker).Assembly
                }
                .Tee(CommandLocator.Instance.RegisterAssemblies)
                .Tee(ScriptEngineRegistry.Instance.RegisterAssemblies);
        }
    }
}