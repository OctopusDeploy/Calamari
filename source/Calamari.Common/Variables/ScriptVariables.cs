using System;
using System.Linq;

namespace Calamari.Common.Variables
{
    public static class ScriptVariables
    {
        public static readonly string ScriptCsPath = "Octopus.Calamari.ScriptCsPath";
        public static readonly string FSharpPath = "Octopus.Calamari.FSharpPath";
        public static readonly string Syntax = "Octopus.Action.Script.Syntax";
        public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
        public static readonly string ScriptFileName = "Octopus.Action.Script.ScriptFileName";

        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }
        
        public static bool IsExcludedFromLocalVariables(string name)
        {
            return name.Contains("[");
        }
        
    }
}