using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Calamari.Azure.ResourceGroups;
using Calamari.Azure.Scripts;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;
using Calamari.Scripting;

namespace Calamari.Azure
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log)
            : base(log)
        { }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);

            builder.RegisterType<TemplateService>();
            builder.RegisterType<ResourceGroupTemplateNormalizer>().As<IResourceGroupTemplateNormalizer>();
            builder.RegisterType<TemplateResolver>().As<ITemplateResolver>().SingleInstance();
            builder.RegisterType<AzureResourceGroupOperator>();
            
        }
        
        protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
        {
            //Calamari.Scripting
            yield return typeof(RunScriptCommand).Assembly;
            //Calamari.Azure
            yield return typeof(Program).Assembly;
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}