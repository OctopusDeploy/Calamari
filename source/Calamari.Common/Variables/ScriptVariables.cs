using System;
using System.Linq;
using Calamari.Integration.Scripting;

namespace Calamari.Common.Variables
{
    public static class ScriptVariables
    {
 
        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        public static ScriptSyntax GetLibraryScriptModuleLanguage(IVariables variables, string variableName)
        {
            var expectedName = variableName.Replace("Octopus.Script.Module[", "Octopus.Script.Module.Language[");
            var syntaxVariable = variables.GetNames().FirstOrDefault(x => x == expectedName);
            if (syntaxVariable == null)
                return ScriptSyntax.PowerShell;
            return (ScriptSyntax) Enum.Parse(typeof(ScriptSyntax), variables[syntaxVariable]);
        }
        
        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }
        
    }
}