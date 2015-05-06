namespace Calamari.Integration.Scripting
{
    public class NixScriptEngineSelector : ScriptEngineSelector
    {
        static readonly string[] SupportedExtensions = new[] { "csx", "sh" };
        public override string[] GetSupportedExtensions()
        {
            return SupportedExtensions;
        }
    }
}