using System;
using System.Linq;
using Calamari.Common.Plumbing;

namespace Calamari.Common.Features.Scripts
{
    public static class ScriptSyntaxHelper
    {
        static readonly ScriptSyntax[] ScriptSyntaxPreferencesNonWindows =
        {
            ScriptSyntax.Bash,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.PowerShell
        };

        static readonly ScriptSyntax[] ScriptSyntaxPreferencesWindows =
        {
            ScriptSyntax.PowerShell,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.Bash
        };

        public static ScriptSyntax GetPreferredScriptSyntaxForEnvironment()
        {
            return CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac
                ? ScriptSyntaxPreferencesNonWindows.First()
                : ScriptSyntaxPreferencesWindows.First();
        }

        public static ScriptSyntax[] GetPreferenceOrderedScriptSyntaxesForEnvironment()
        {
            return CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac
                ? ScriptSyntaxPreferencesNonWindows
                : ScriptSyntaxPreferencesWindows;
        }
    }
}