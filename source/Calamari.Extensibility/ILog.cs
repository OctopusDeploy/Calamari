namespace Calamari
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
        string Link(string uri, string description = null);
        void SetOutputVariable(string name, string value, bool isSensitive = false);
        void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false);
        void NewOctopusArtifact(string fullPath, string name, long fileLength);
    }
}