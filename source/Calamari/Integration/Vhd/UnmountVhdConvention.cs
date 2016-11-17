using System;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.Vhd
{
    public class UnmountVhdConvention : IInstallConvention, IRollbackConvention
    {
        private readonly ICalamariFileSystem fileSystem;

        public UnmountVhdConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!Vhd.FeatureIsOn(deployment))
                return;

            var vhdPath = Vhd.FindSingleVhdInFolder(fileSystem, deployment.CurrentDirectory);

            Vhd.Unmount(vhdPath);
        }

        public void Rollback(RunningDeployment deployment)
        {
            Install(deployment);
        }

        public void Cleanup(RunningDeployment deployment)
        {
        }
    }
}