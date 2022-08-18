using System;
using System.Diagnostics;
using System.Linq;
using Calamari.Common.Plumbing;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class ProcessFinder : IProcessFinder
    {
        public bool ProcessIsRunning(int processId, string processName)
        {
            // borrowed from https://github.com/sillsdev/libpalaso/blob/7c7e5eed0a3d9c8a961b01887cbdebbf1b63b899/SIL.Core/IO/FileLock/SimpleFileLock.cs (Apache 2.0 license)
            Process process;
            try
            {
                // First, look for a process with this processId
                process = Process.GetProcesses().FirstOrDefault(x => x.Id == processId);
            }
            catch (NotSupportedException)
            {
                //FreeBSD does not support EnumProcesses
                //assume that the process is running
                return true;
            }

            // If there is no process with this processId, it is not running.
            if (process == null)
                return false;

            // Next, check for a match on processName.
            var isRunning = process.ProcessName == processName;

            // If a match was found or this is running on Windows, this is as far as we need to go.
            if (isRunning || CalamariEnvironment.IsRunningOnWindows)
                return isRunning;

            // We need to look a little deeper on Linux.

            // If the name of the process is not "mono" or does not start with "mono-", this is not
            // a mono application, and therefore this is not the process we are looking for.
            if (process.ProcessName.ToLower() != "mono" && !process.ProcessName.ToLower().StartsWith("mono-"))
                return false;

            // The mono application will have a module with the same name as the process, with ".exe" added.
            var moduleName = processName.ToLower() + ".exe";
            return process.Modules.Cast<ProcessModule>().Any(mod => mod.ModuleName.ToLower() == moduleName);
        }
    }
}