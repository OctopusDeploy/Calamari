using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Calamari.Integration.FileSystem;
using System.Runtime.InteropServices; // Needed for dotnetcore
using System.Linq;
#if CAN_GET_PHYSICAL_MEMORY
using Microsoft.VisualBasic.Devices;
#endif

namespace Calamari.Util.Environments /* This is in the 'Environments' namespace to avoid collisions with CrossPlatformExtensions */
{
    public static class EnvironmentHelper
    {
        public static string[] SafelyGetEnvironmentInformation()
        {
            var envVars = SafelyGetEnvironmentVars()
                .Concat(SafelyGetPathVars())
                .Concat(SafelyGetProcessVars())
                .Concat(SafelyGetComputerInfoVars());
            return envVars.ToArray();
        }

        static IEnumerable<string> SafelyGetEnvironmentVars()
        {
            try
            {
                return GetEnvironmentVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetEnvironmentVars()
        {
#if NET40
            yield return $"OperatingSystem: {Environment.OSVersion.ToString()}";
            yield return $"OsBitVersion: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}";
            yield return $"Is64BitProcess: {Environment.Is64BitProcess.ToString()}";
#else
            yield return $"OperatingSystem: {System.Runtime.InteropServices.RuntimeInformation.OSDescription.ToString()}";
            yield return $"OsBitVersion: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()}";
#endif
            yield return $"CurrentUser: {Environment.UserName}";
            yield return $"MachineName: {Environment.MachineName}";
            yield return $"ProcessorCount: {Environment.ProcessorCount.ToString()}";
        }

        static IEnumerable<string> SafelyGetPathVars()
        {
            try
            {
                return GetPathVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetPathVars()
        {
            yield return $"CurrentDirectory: {Directory.GetCurrentDirectory()}";
            yield return $"TempDirectory: {Path.GetTempPath()}";
        }

        static IEnumerable<string> SafelyGetProcessVars()
        {
            try
            {
                return GetProcessVars();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetProcessVars()
        {
            yield return $"HostProcessName: {Process.GetCurrentProcess().ProcessName}";
        }

        static IEnumerable<string> SafelyGetComputerInfoVars()
        {
            try
            {
                return Enumerable.Empty<string>();
            }
            catch
            {
                // Fail silently
                return Enumerable.Empty<string>();
            }
        }
        static IEnumerable<string> GetComputerInfoVars()
        {
#if CAN_GET_PHYSICAL_MEMORY
            var computerInfo = new ComputerInfo();
            yield return $"TotalPhysicalMemory: {computerInfo.TotalPhysicalMemory.ToFileSizeString()}";
            yield return $"AvailablePhysicalMemory: {computerInfo.AvailablePhysicalMemory.ToFileSizeString()}";
#else
            yield break;
#endif
        }
    }
}
