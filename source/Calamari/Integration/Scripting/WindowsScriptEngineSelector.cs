namespace Calamari.Integration.Scripting
{
    public class WindowsScriptEngineSelector : ScriptEngineSelector
    {
        static readonly string[] SupportedExtensions = new[] { "csx", "ps1" };
        public override string[] GetSupportedExtensions()
        {
            return SupportedExtensions;
        }
    }
}