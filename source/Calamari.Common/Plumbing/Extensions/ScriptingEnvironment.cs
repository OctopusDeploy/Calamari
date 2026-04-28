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