using Calamari.Util.Environments;
using System.Reflection;

namespace Calamari.Tests
{
    class Program
    {
        //This is a shell around Calamari.exe so we can use it in .net core testing, since in .net core when we reference the
        //Calamari project we only get the dll, not the exe
        public static int Main(string[] args)
        {
            var program = new Calamari.Program("Calamari", typeof(Calamari.Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation());
            return program.Execute(args);
        }
    }
}
