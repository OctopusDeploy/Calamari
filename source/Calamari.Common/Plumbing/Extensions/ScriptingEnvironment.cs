using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;

namespace Calamari.Common.Plumbing.Extensions
{
    public class ScriptingEnvironment
    {
        public static bool IsNetFramework()
        {
#if NETFRAMEWORK
            return true;
#else
            return false;
#endif
        }

        public static bool IsNet45OrNewer()
        {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        public static bool IsRunningOnMono()
        {
            var monoRuntime = Type.GetType("Mono.Runtime");
            return monoRuntime != null;
        }

        public static Version GetMonoVersion()
        {
            // A bit hacky, but this is what Mono community seems to be using:
            // http://stackoverflow.com/questions/8413922/programmatically-determining-mono-runtime-version

            var monoRuntime = Type.GetType("Mono.Runtime");
            if (monoRuntime == null)
                throw new MonoVersionCanNotBeDeterminedException("It looks like the code is not running on Mono.");

            var dispalayNameMethod = monoRuntime.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            if (dispalayNameMethod == null)
                throw new MonoVersionCanNotBeDeterminedException("Mono.Runtime.GetDisplayName can not be found.");

            var displayName = dispalayNameMethod.Invoke(null, null).ToString();
            var match = Regex.Match(displayName, @"^\d+\.\d+.\d+");
            if (match == null || !match.Success || string.IsNullOrEmpty(match.Value))
                throw new MonoVersionCanNotBeDeterminedException($"Display name does not seem to include version number. Retrieved value: {displayName}.");

            return Version.Parse(match.Value);
        }

        public static Version SafelyGetPowerShellVersion()
        {
            try
            {
                foreach (var cmd in new[] { "powershell.exe", "pwsh.exe", "pwsh" })
                {
                    try
                    {
                        var stdOut = new StringBuilder();
                        var stdError = new StringBuilder();
                        var result = SilentProcessRunner.ExecuteCommand(
                                                                        cmd,
                                                                        $"-command \"{"$PSVersionTable.PSVersion.ToString()"}\"",
                                                                        Environment.CurrentDirectory,
                                                                        s => stdOut.AppendLine(s),
                                                                        s => stdError.AppendLine(s));
                        if (result.ExitCode == 0)
                            return Version.Parse(stdOut.ToString());
                    }
                    catch
                    {
                        //maybe pwsh will work?
                    }
                }
            }
            catch
            {
                //silently ignore it - we dont want to
            }

            return Version.Parse("0.0.0");
        }
    }
}