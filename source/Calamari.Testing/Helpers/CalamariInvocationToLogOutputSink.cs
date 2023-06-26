using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Testing.Helpers;

public class CalamariInvocationToLogOutputSink : ICommandInvocationOutputSink
{
    readonly ILog log;
    private readonly ServiceMessageParser serviceMessageParser;
    private LogLevel logLevel = LogLevel.Info;

    public CalamariInvocationToLogOutputSink(ILog log)
    {
        this.log = log;
        serviceMessageParser = new ServiceMessageParser(ProcessServiceMessage);
    }

    private void ProcessServiceMessage(ServiceMessage serviceMessage)
    {
        logLevel = serviceMessage.Name switch
        {
            "stdout-verbose" => LogLevel.Verbose,
            "stdout-default" => LogLevel.Info,
            "stdout-warning" => LogLevel.Warn,
            _ => logLevel
        };

        log.WriteServiceMessage(serviceMessage);
    }

    public void WriteInfo(string line)
    {
        if (serviceMessageParser.Parse(line))
            return;

        switch (logLevel)
        {
            case LogLevel.Verbose:
                log.Verbose(line);
                break;
            case LogLevel.Warn:
                log.Warn(line);
                break;
            case LogLevel.Info:
            default:
                log.Info(line);
                break;
        }
    }

    public void WriteError(string line)
    {
        log.Error(line);
    }

    private enum LogLevel
    {
        Verbose,
        Info,
        Warn
    }
}