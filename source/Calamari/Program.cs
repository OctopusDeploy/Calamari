using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Calamari.AzureScripting;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Scripting;

namespace Calamari.AzureCloudService
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

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);
            builder.RegisterType<AzurePackageUploader>().AsSelf();
            builder.RegisterType<AzureAccount>().AsSelf();
        }

        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return typeof(AzureContextScriptWrapper).Assembly;
            yield return typeof(RunScriptCommand).Assembly;
            yield return typeof(Program).Assembly;
        }
    }
}