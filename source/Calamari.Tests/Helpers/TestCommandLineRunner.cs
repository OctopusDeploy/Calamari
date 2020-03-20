using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Calamari.Variables;

namespace Calamari.Tests.Helpers
{
    public class TestCommandLineRunner : CommandLineRunner
    {
        readonly IVariables variables;

        public TestCommandLineRunner(IVariables variables) : base(variables)
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