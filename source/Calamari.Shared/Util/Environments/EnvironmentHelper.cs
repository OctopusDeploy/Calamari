using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices; // Needed for dotnetcore
using System.Linq;

namespace Calamari.Util.Environments
    /* This is in the 'Environments' namespace to avoid collisions with CrossPlatformExtensions */
{
    public static class EnvironmentHelper
    {
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = GetEnvironmentVars()
                .Concat(GetPathVars())
                .Concat(GetProcessVars());
            return envVars.ToArray();
        }

        public static void SetEnvironmentVariable(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }

        private static string SafelyGet(Func<string> thingToGet)
        {
            try
            {
                return thingToGet.Invoke();
            }
            catch (Exception)
            {
                return "Unable to retrieve environment information.";
            }
        }

        static IEnumerable<string> GetEnvironmentVars()
        {
            yield return SafelyGet(() => $"OperatingSystem: {Environment.OSVersion}");
            yield return SafelyGet(() => $"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            yield return SafelyGet(() => $"Is64BitProcess: {Environment.Is64BitProcess}");
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                yield return SafelyGet(() => $"Running on Mono: {CalamariEnvironment.IsRunningOnMono}");
            if(CalamariEnvironment.IsRunningOnWindows)
                yield return SafelyGet(() => $"CurrentUser: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");
            else
                yield return SafelyGet(() => $"CurrentUser: {Environment.UserName}");
            yield return SafelyGet(() => $"MachineName: {Environment.MachineName}");
            yield return SafelyGet(() => $"ProcessorCount: {Environment.ProcessorCount}");
        }

        static IEnumerable<string> GetPathVars()
        {
            yield return SafelyGet(() => $"CurrentDirectory: {Directory.GetCurrentDirectory()}");
            yield return SafelyGet(() => $"TempDirectory: {Path.GetTempPath()}");
        }

        static IEnumerable<string> GetProcessVars()
        {
            yield return SafelyGet(() => $"HostProcessName: {Process.GetCurrentProcess().ProcessName}");
        }
    }
}
