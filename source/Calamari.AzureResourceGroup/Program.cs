using System.Collections.Generic;
using System.IO.Abstractions;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Threading.Tasks;
using Autofac;
using Calamari.AzureScripting;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;
using Microsoft.Extensions.DependencyInjection;
using Autofac.Extensions.DependencyInjection;

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
            builder.RegisterType<TemplateResolver>().As<ITemplateResolver>().SingleInstance();
            builder.RegisterType<BicepTemplateCompiler>().As<BicepTemplateCompiler>().SingleInstance();
            builder.Populate(new ServiceCollection().AddBicep());
        }

        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            yield return typeof(AzureContextScriptWrapper).Assembly;
            yield return typeof(Program).Assembly;
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}