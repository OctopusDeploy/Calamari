using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Testing.Helpers
{
    public class TestCommandLineRunner : CommandLineRunner
    {
        private readonly ILog log;
        readonly IVariables variables;

        public TestCommandLineRunner(ILog log, IVariables variables) : base(log, variables)
        {
            this.log = log;
            this.variables = variables;
            Output = new CaptureCommandInvocationOutputSink();
        }

        public CaptureCommandInvocationOutputSink Output { get; }

        protected override List<ICommandInvocationOutputSink> GetCommandOutputs(CommandLineInvocation invocation)
        {
            var outputSinks = new List<ICommandInvocationOutputSink>()
            {
                Output,
                new ServiceMessageCommandInvocationOutputSink(variables)
            };

            if (invocation.OutputToLog)
            {
                outputSinks.Add(new LogCommandInvocationOutputSink(log, invocation.OutputAsVerbose));
            }

            return outputSinks;
        }
    }
}