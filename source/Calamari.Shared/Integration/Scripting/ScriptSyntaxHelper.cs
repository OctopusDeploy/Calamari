using System.Linq;

namespace Calamari.Integration.Scripting
{
    public static class ScriptSyntaxHelper
    {
        private static readonly ScriptSyntax[] ScriptSyntaxPreferencesNonWindows = new[]
        {
            ScriptSyntax.Bash,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.PowerShell
        };

        private static readonly ScriptSyntax[] ScriptSyntaxPreferencesWindows = new[]
        {
            ScriptSyntax.PowerShell,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.Bash
        };

        public static ScriptSyntax GetPreferredScriptSyntaxForEnvironment()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? ScriptSyntaxPreferencesNonWindows.First()
                : ScriptSyntaxPreferencesWindows.First();
        }

        public static ScriptSyntax[] GetPreferenceOrderedScriptSyntaxesForEnvironment()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? ScriptSyntaxPreferencesNonWindows
                : ScriptSyntaxPreferencesWindows;
        }
    }
}