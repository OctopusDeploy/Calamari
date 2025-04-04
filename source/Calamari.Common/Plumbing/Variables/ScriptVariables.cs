using System;
using System.Linq;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Plumbing.Variables
{
    public static class ScriptVariables
    {
        public static readonly string Syntax = "Octopus.Action.Script.Syntax";
        public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
        public static readonly string ScriptFileName = "Octopus.Action.Script.ScriptFileName";
        public static readonly string ScriptParameters = "Octopus.Action.Script.ScriptParameters";
        public static readonly string ScriptSource = "Octopus.Action.Script.ScriptSource";

        public static readonly string UseDotnetScript = "Octopus.Action.Script.CSharp.UseDotnetScript";

        public static class ScriptSourceOptions
        {
            public const string Package = "Package";
            public const string Inline = "Inline";
            public const string Core = "Core";
            public const string GitRepository = "GitRepository";
        }

        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }
        
        public static bool IsBuildInformationVariable(string variableName)
        {
            return variableName.StartsWith("Octopus.Deployment.PackageBuildInformation") || variableName.StartsWith("Octopus.Deployment.PackageBuildMetadata");
        }

        public static bool IsExcludedFromLocalVariables(string name)
        {
            return name.Contains("[");
        }

        public static ScriptSyntax GetLibraryScriptModuleLanguage(IVariables variables, string variableName)
        {
            var expectedName = variableName.Replace("Octopus.Script.Module[", "Octopus.Script.Module.Language[");
            var syntaxVariable = variables.GetNames().FirstOrDefault(x => x == expectedName);
            if (syntaxVariable == null)
                return ScriptSyntax.PowerShell;
            return (ScriptSyntax)Enum.Parse(typeof(ScriptSyntax), variables[syntaxVariable]);
        }
    }
}