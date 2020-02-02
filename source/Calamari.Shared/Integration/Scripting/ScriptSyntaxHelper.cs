namespace Calamari.Integration.Scripting
{
    public static class ScriptSyntaxHelper
    {
        public static ScriptSyntax GetPreferredScriptSyntaxForEnvironment()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? ScriptSyntax.Bash
                : ScriptSyntax.PowerShell;
        }       
    }
}