using System;
<<<<<<< HEAD
using System.Threading.Tasks;
using Autofac;
using Calamari.AzureAppService.Azure;
=======
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureScripting;
>>>>>>> release/2026.2
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;


namespace Calamari.AzureAppService
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

<<<<<<< HEAD
        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);

            builder.RegisterType<AzureWebAppDiscoverer>().As<IAzureWebAppDiscoverer>();
            builder.RegisterType<AzureAppServiceContainerConfigurer>().As<IAzureAppServiceContainerConfigurer>();
=======
        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return typeof(AzureContextScriptWrapper).Assembly;
            yield return typeof(Program).Assembly;
>>>>>>> release/2026.2
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}
