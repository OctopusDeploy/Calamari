using System;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.Vhd
{
    public class MountVhdConvention : IInstallConvention
    {
        private readonly ICalamariFileSystem fileSystem;

        public MountVhdConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag("###############Is Vhd TODO####################"))
                return;

            var vhds = fileSystem.EnumerateFiles(deployment.CurrentDirectory, "*.vhd", "*.vhdx").ToArray();
            if (vhds.Length == 0)
            {
                throw new Exception("No VHD or VHDX file found in package.");
            }
            if (vhds.Length > 1)
            {
                throw new Exception("More than one VHD or VHDX file found in package. Only bundle a single disk image per package.");
            }

            Vhd.Mount(vhds.Single(), Path.Combine(deployment.CurrentDirectory, "mount"));
        }
    }
}