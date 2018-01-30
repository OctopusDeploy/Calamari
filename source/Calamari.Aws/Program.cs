using System;
using Calamari.Commands.Support;
using Calamari.Util.Environments;

namespace Calamari.Aws
{
    class Program : Calamari.Program
    {
        public Program() : base("Calamari.Aws", typeof(Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation())
        {
                       
        }

        static int Main(string[] args)
        {
            var program = new Program();
            return program.Execute(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Calamari.Program).Assembly, typeof(Program).Assembly);
        }
    }
}