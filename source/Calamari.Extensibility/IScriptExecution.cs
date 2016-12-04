using Calamari.Extensibility.Scripting;

namespace Calamari.Extensibility
{
    public interface IScriptExecution
    {
        /// <summary>
        /// Returns list of file extensions that can be executed with the available script runners.
        /// </summary>
        /// <example>sh, fsx, ps1</example>
        /// <returns></returns>
        string[] SupportedExtensions { get; }

        ICommandResult InvokeFromFile(string scriptFile, string scriptParameters);
    }
}