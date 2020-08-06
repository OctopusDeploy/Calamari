using System;
using System.Threading.Tasks;
using Autofac;
using Calamari.AzureScripting;
using Calamari.Common;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;

namespace Calamari.AzureResourceGroup
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);

            builder.RegisterType<TemplateService>();
            builder.RegisterType<ResourceGroupTemplateNormalizer>().As<IResourceGroupTemplateNormalizer>();
            builder.RegisterType<AzureContextScriptWrapper>().As<IScriptWrapper>().SingleInstance();
            builder.RegisterType<TemplateResolver>().As<ITemplateResolver>().SingleInstance();
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}