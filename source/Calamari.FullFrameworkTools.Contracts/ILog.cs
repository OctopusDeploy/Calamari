namespace Calamari.FullFrameworkTools.Contracts;

public interface ILog
{
    void Verbose(string message);
    void Info(string message);
    
    void ErrorFormat(string messageFormat, params object[] args);
}