namespace Calamari.Integration.Scripting
{
    public interface IScriptEngineSelector
    {
        string[] GetSupportedExtensions();
        IScriptEngine SelectEngine(string scriptFile);
    }
}