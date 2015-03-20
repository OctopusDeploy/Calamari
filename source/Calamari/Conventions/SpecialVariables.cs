namespace Calamari.Conventions
{
    public static class SpecialVariables
    {
        public const string LastErrorMessage = "OctopusLastErrorMessage";
        public const string LastError = "OctopusLastError";

        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        public static bool IsExcludedFromLocalVariables(string name)
        {
            return name.Contains("[");
        }

        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }
    }
}