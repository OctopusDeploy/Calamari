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
            Output = new CaptureCommandOutput();
        }

        public CaptureCommandOutput Output { get; }

        protected override List<ICommandOutput> GetCommandOutputs(CommandLineInvocation invocation)
            => new List<ICommandOutput>()
            {
                Output,
                new ServiceMessageCommandOutput(variables)
            };
    }
}