using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Tests.Helpers;

namespace Calamari.Tests
{
    public static class TestProgramWrapper
    {
        //This is a shell around Calamari.exe so we can use it in .net core testing, since in .net core when we reference the
        //Calamari project we only get the dll, not the exe
        public static async Task<int> Main(string[] args)
        {
            var program = new TestProgram(ConsoleLog.Instance);
            return await program.Execute(args);
        }
    }
}