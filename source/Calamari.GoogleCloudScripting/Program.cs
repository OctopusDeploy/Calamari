using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Plumbing.Logging;
using Calamari.Scripting;

namespace Calamari.GoogleCloudScripting
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

        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return typeof(RunScriptCommand).Assembly;
            yield return typeof(Program).Assembly;
        }
    }
}