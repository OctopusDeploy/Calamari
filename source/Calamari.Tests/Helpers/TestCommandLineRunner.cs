using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Variables;

namespace Calamari.Tests.Helpers
{
    public class TestCommandLineRunner : CommandLineRunner
    {
        public TestCommandLineRunner(IVariables variables) : base(variables)
        {
            Output = new CaptureCommandOutput();
        }

        public CaptureCommandOutput Output { get; }

        protected override List<ICommandOutput> GetCommandOutputs(CommandLineInvocation invocation)
            => new List<ICommandOutput>() { Output };
    }
}