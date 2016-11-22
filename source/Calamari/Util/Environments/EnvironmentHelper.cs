using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Calamari.Integration.FileSystem;
using System.Runtime.InteropServices;
#if VISUALBASIC_SUPPORT
using Microsoft.VisualBasic.Devices;
#endif

namespace Calamari.Util.Environments /* This is in the 'Environments' namespace to avoid collisions with CrossPlatformExtensions - long story */
{
    public static class EnvironmentHelper
    {
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = new List<string>();
            SafelyAddEnvironmentVarsToList(ref envVars);
            SafelyAddPathVarsToList(ref envVars);
            SafelyAddProcessVarsToList(ref envVars);
            SafelyAddComputerInfoVarsToList(ref envVars);
            return envVars.ToArray();
        }

        static void SafelyAddEnvironmentVarsToList(ref List<string> envVars)
        {
            try
            {
#if NET40
                envVars.Add($"OperatingSystem: {Environment.OSVersion.ToString()}");
                envVars.Add($"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
                envVars.Add($"CurrentUser: {Environment.UserName}");
                envVars.Add($"Is64BitProcess: {Environment.Is64BitProcess.ToString()}");
#else
                envVars.Add($"OperatingSystem: {System.Runtime.InteropServices.RuntimeInformation.OSDescription.ToString()}");
                envVars.Add($"OperatingSystem: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()}");
#endif
                envVars.Add($"MachineName: {Environment.MachineName}");
                envVars.Add($"ProcessorCount: {Environment.ProcessorCount.ToString()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddPathVarsToList(ref List<string> envVars)
        {
            try
            {
                envVars.Add($"CurrentDirectory: {Directory.GetCurrentDirectory()}");
                envVars.Add($"TempDirectory: {Path.GetTempPath()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddProcessVarsToList(ref List<string> envVars)
        {
            try
            {
                envVars.Add($"HostProcessName: {Process.GetCurrentProcess().ToString()}");
            }
            catch
            {
                // silently fail.
            }
        }

        static void SafelyAddComputerInfoVarsToList(ref List<string> envVars)
        {
#if VISUALBASIC_SUPPORT
            try
            {
                var computerInfo = new ComputerInfo();
                envVars.Add($"TotalPhysicalMemory: {computerInfo.TotalPhysicalMemory.ToFileSizeString()}");
                envVars.Add($"AvailablePhysicalMemory: {computerInfo.AvailablePhysicalMemory.ToFileSizeString()}");
            }
            catch
            {
                // silently fail.
            }
#endif
        }
    }
}
