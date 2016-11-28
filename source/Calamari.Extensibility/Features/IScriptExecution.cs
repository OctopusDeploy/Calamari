namespace Calamari.Extensibility.Features
{
    public interface IScriptExecution
    {
        void Invoke(string scriptFile, string scriptParameters);
    }
}