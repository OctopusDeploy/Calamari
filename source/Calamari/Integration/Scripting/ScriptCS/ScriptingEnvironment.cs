using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Calamari.Integration.Scripting.ScriptCS
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

        public static Version GetMonoVersion()
        {
            // A bit hacky, but this is what Mono community seems to be using: 
            // http://stackoverflow.com/questions/8413922/programmatically-determining-mono-runtime-version

            var monoRuntime = Type.GetType("Mono.Runtime");
            if (monoRuntime == null) throw new MonoVersionCanNotBeDeterminedException("It looks like the code is not running on Mono.");

            var dispalayNameMethod = monoRuntime.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            if (dispalayNameMethod == null) throw new MonoVersionCanNotBeDeterminedException("Mono.Runtime.GetDisplayName can not be found.");
            
            var displayName = dispalayNameMethod.Invoke(null, null).ToString();
            var match = Regex.Match(displayName, @"^\d+\.\d+.\d+");
            if (match == null || !match.Success || string.IsNullOrEmpty(match.Value))
                throw new MonoVersionCanNotBeDeterminedException($"Display name does not seem to include version number. Retrieved value: {displayName}.");

            return Version.Parse(match.Value);           
        }
    }
}