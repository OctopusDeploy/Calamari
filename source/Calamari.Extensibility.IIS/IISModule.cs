using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.Extensibility.IIS
{
    public class IISModule : IModule
    {
        public void Register(ICalamariContainer container)
        {
            container.RegisterInstance<IInternetInformationServer>(new InternetInformationServer(container.Resolve<ILog>()));
        }
    }
}
