using System.Threading.Tasks;
using Autofac;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureWebApp
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);
            builder.RegisterType<ResourceManagerPublishProfileProvider>().SingleInstance();
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}