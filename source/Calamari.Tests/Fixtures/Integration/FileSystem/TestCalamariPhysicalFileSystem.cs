using System;
using System.IO;
using System.Reflection;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.FileSystem;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    public class TestCalamariPhysicalFileSystem : CalamariPhysicalFileSystem
    {
        public new static ICalamariFileSystem GetPhysicalFileSystem()
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                return new TestNixCalamariPhysicalFileSystem();
            }

            return new TestWindowsPhysicalFileSystem();
        }

        // public void SetFileBasePath(string basePath) => File = new TestFile(basePath);
        
        public override bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes)
        {
            throw new NotImplementedException("*testing* this is in TestCalamariPhysicalFileSystem");
        }

        public override bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes)
        {
            throw new NotImplementedException("*testing* this is in TestCalamariPhysicalFileSystem");
        }
        private class TestNixCalamariPhysicalFileSystem : NixCalamariPhysicalFileSystem, ICalamariFileSystem
        {
            public new string CreateTemporaryDirectory()
            {
                var path = Path.Combine("/tmp", Guid.NewGuid().ToString());

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        }
        private class TestWindowsPhysicalFileSystem : WindowsPhysicalFileSystem, ICalamariFileSystem
        {
            public new string CreateTemporaryDirectory()
            {
               var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                path = Path.Combine(path, Assembly.GetEntryAssembly()?.GetName().Name ?? Guid.NewGuid().ToString());

                path = Path.Combine(path, Guid.NewGuid().ToString());

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
        }
    }
}