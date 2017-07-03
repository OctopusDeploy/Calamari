using Calamari.Commands.Support;
using Calamari.Util.Environments;
using System;

namespace Calamari.Java
{
    class Program : Calamari.Program
    {
        public Program() 
            : base("Calamari.Java", typeof(Java.Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation())
        {
        }

        static int Main(string[] args)
        {
            var program = new Java.Program();
            return program.Execute(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Calamari.Program).Assembly, typeof(Java.Program).Assembly);
        }
    }
}
