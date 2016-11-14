using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.Shared.Convention
{
    public interface IModule
    {
        void Register(ICalamariContainer calamariContainer);
    }

    public interface ICalamariContainer
    {
        // builder.RegisterType<ArtifactStore>().As<IArtifactStore>().InstancePerLifetimeScope();
        void RegisterInstance<TType>(TType instance);
    }
}
