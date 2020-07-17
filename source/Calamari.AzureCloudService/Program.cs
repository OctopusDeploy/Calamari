using System.Threading.Tasks;
using Autofac;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureCloudService
{
    public class Program : Calamari.CommonTemp.CalamariFlavourProgramAsync
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
    }
}