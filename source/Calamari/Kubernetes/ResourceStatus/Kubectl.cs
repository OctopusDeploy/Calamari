using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IKubectl
    {
        string Get(string kind, string name, string @namespace);
        string GetAll(string kind, string @namespace);
    }
    
    public class Kubectl : IKubectl
    {
        private readonly string kubectl;
        private readonly ICommandLineRunner commandLineRunner;
        
        public Kubectl(IVariables variables, ICommandLineRunner commandLineRunner)
        {
            kubectl = variables.Get(SpecialVariables.CustomKubectlExecutable, "kubectl");
            this.commandLineRunner = commandLineRunner;
        }

        public string Get(string kind, string name, string @namespace)
        {
            return ExecuteCommandAndReturnOutput(kubectl,
                new[] {"get", kind, name, "-o json", $"-n {@namespace}"});
        }
    
        public string GetAll(string kind, string @namespace)
        {
            return ExecuteCommandAndReturnOutput(kubectl,
                new[] {"get", kind, "-o json", $"-n {@namespace}"});
        }
    
        private string ExecuteCommandAndReturnOutput(string exe, string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput
            };
    
            commandLineRunner.Execute(invocation);
    
            return captureCommandOutput.Messages
                .Where(m => m.Level == Level.Info)
                .Select(m => m.Text)
                .Join("");
        }
    }
}