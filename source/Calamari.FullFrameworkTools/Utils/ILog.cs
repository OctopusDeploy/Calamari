using System;

namespace Calamari.FullFrameworkTools.Command;

public interface ILog
{
    void Verbose(string message);
    void Error(string message);
    void Info(string message);
    void Fatal(Exception exception);
    void Result(object response);
}