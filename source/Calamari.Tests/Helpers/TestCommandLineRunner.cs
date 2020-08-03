using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Processes;

namespace Calamari.Tests.Helpers
{
    public class TestCommandLineRunner : CommandLineRunner
    {
        readonly IVariables variables;

        public TestCommandLineRunner(ILog log, IVariables variables) : base(log, variables)
        {
            this.variables = variables;
            Output = new CaptureCommandInvocationOutputSink();
        }

        public CaptureCommandInvocationOutputSink Output { get; }

        protected override List<ICommandInvocationOutputSink> GetCommandOutputs(CommandLineInvocation invocation)
            => new List<ICommandInvocationOutputSink>()
            {
                Output,
                new ServiceMessageCommandInvocationOutputSink(variables)
            };
    }
}