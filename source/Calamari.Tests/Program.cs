using Autofac;

namespace Calamari.Tests
{
    class Program
    {
        //This is a shell around Calamari.exe so we can use it in .net core testing, since in .net core when we reference the
        //Calamari project we only get the dll, not the exe
        public static int Main(string[] args)
        {
            using (var container = Calamari.Program.BuildContainer())
            {
                return container.Resolve<Calamari.Program>().Execute(args);
            }
        }
    }
}
