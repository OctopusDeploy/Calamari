using Octostache;

namespace Calamari.Shared
{
    public interface ILog
    {
        void Verbose(string message);
        void VerboseFormat(string message, params object[] args);
        void Info(string message);
        void InfoFormat(string message, params object[] args);
        void Warn(string message);
        void WarnFormat(string message, params object[] args);
        void Error(string message);
        void ErrorFormat(string message, params object[] args);

        void SetOutputVariable(string name, string value);
        void SetOutputVariable(string name, string value, VariableDictionary variables);
    }


    public static class Log
    {
        public static ILog Instance { get; internal set; }
    }
}