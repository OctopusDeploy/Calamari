using System.Collections;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Authentication
{
    public class BaseSetupKubectlAuthenticationFixture
    {
        protected readonly string workingDirectory = Path.Combine("working", "directory");
        protected const string Namespace = "my-cool-namespace";

        protected IVariables variables;
        protected ILog log;
        protected ICommandLineRunner commandLineRunner;
        protected IKubectl kubectl;
        protected ICalamariFileSystem fileSystem;
        protected Dictionary<string, string> environmentVars;

        protected Invocations invocations;

        [SetUp]
        public void BaseSetup()
        {
            invocations = new Invocations();
            invocations.AddLogMessageFor("which", "kubelogin", "kubelogin");
            invocations.AddLogMessageFor("where", "kubelogin", "kubelogin");

            variables = new CalamariVariables();

            log = Substitute.For<ILog>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Any<CommandLineInvocation>())
                .Returns(
                    x =>
                    {
                        var invocation = x.Arg<CommandLineInvocation>();
                        var isSuccess = true;
                        string logMessage = null;
                        if (invocation.Executable != "chmod")
                        {
                            isSuccess = invocations.TryAdd(invocation.Executable, invocation.Arguments, out logMessage);
                        }
                        if (logMessage != null)
                            invocation.AdditionalInvocationOutputSink?.WriteInfo(logMessage);
                        return new CommandResult(
                            invocation.Executable,
                            isSuccess ? 0 : 1,
                            workingDirectory: workingDirectory);
                    });

            kubectl = Substitute.For<IKubectl>();
            kubectl.ExecutableLocation.Returns("kubectl");
            kubectl.When(x => x.ExecuteCommandAndAssertSuccess(Arg.Any<string[]>()))
                .Do(
                    x =>
                    {
                        var args = x.Arg<string[]>();
                        if (args != null)
                            invocations.TryAdd("kubectl", string.Join(" ", args), out var _);
                    });
            kubectl.ExecuteCommandWithVerboseLoggingOnly(Arg.Any<string[]>())
                .Returns(
                    x =>
                    {
                        var args = x.Arg<string[]>();
                        var isSuccess = true;

                        if (args != null)
                            isSuccess = invocations.TryAdd("kubectl", string.Join(" ", args), out _);

                        return new CommandResult("kubectl", isSuccess ? 0 : 1);
                    });

            fileSystem = Substitute.For<ICalamariFileSystem>();
            environmentVars = new Dictionary<string, string>();
        }

        protected class Invocations : IReadOnlyList<(string Executable, string Arguments)>
        {
            private readonly List<(string Executable, string Arguments)> invocations = new List<(string Executable, string Arguments)>();
            private readonly List<(string Executable, string Arguments)> failFor = new List<(string Executable, string Arguments)>();
            private readonly Dictionary<(string, string), string> logMessageMap = new Dictionary<(string, string), string>();

            public bool TryAdd(string executable, string arguments, out string logMessage)
            {
                invocations.Add((executable, arguments));
                logMessageMap.TryGetValue((executable, arguments), out logMessage);
                return !failFor.Contains((executable, arguments));
            }

            public void FailFor(string executable, string arguments)
            {
                failFor.Add((executable, arguments));
            }

            public void AddLogMessageFor(string executable, string arguments, string logMessage)
            {
                logMessageMap[(executable, arguments)] = logMessage;
            }

            public IEnumerator<(string Executable, string Arguments)> GetEnumerator()
            {
                return invocations.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int Count => invocations.Count;

            public (string Executable, string Arguments) this[int index] => invocations[index];
        }
    }
}