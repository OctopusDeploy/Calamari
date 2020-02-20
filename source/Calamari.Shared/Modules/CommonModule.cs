using System.Collections.Generic;
using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using System.IO;
using System.Linq;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Octostache;
using Module = Autofac.Module;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register common objects
    /// </summary>
    public class CommonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>();
            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().InstancePerLifetimeScope();
            builder.RegisterType<LogWrapper>().As<ILog>().InstancePerLifetimeScope();
        }
    }
}
