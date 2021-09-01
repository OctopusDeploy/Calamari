using System;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.NamingIsHard
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}