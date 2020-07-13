using Calamari.Common;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureWebApp
{
    public class Program : CalamariFlavourProgram
    {
        public Program(ILog log) : base(log)
        {
        }

        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}