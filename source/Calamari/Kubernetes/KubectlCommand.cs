using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes;

public interface IKubectlCommand
{
    string Get(string kind, string name, string @namespace, ICommandLineRunner commandLineRunner);
    string GetAll(string kind, string @namespace, ICommandLineRunner commandLineRunner);
}

public class KubectlCommand : IKubectlCommand
{
    public string Get(string kind, string name, string @namespace, ICommandLineRunner commandLineRunner)
    {
        return ExecuteCommandAndReturnOutput("kubectl",
            new[] {"get", kind, name, "-o json", $"-n {@namespace}"}, commandLineRunner);
    }

    public string GetAll(string kind, string @namespace, ICommandLineRunner commandLineRunner)
    {
        return ExecuteCommandAndReturnOutput("kubectl",
            new[] {"get", kind, "-o json", $"-n {@namespace}"}, commandLineRunner);
    }

    private static string ExecuteCommandAndReturnOutput(string exe, string[] arguments, ICommandLineRunner commandLineRunner)
    {
        var captureCommandOutput = new CaptureCommandOutput();
        var invocation = new CommandLineInvocation(exe, arguments)
        {
            OutputAsVerbose = false,
            OutputToLog = false,
            AdditionalInvocationOutputSink = captureCommandOutput
        };

        commandLineRunner.Execute(invocation);

        return captureCommandOutput.Messages.Where(m => m.Level == Level.Info).Select(m => m.Text).ToArray().Join("");
    }
}