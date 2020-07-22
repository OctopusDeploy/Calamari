using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.NewPipeline
{
    [TestFixture]
    public class PipelineFixture
    {
        [Test]
        public async Task EnsureAllContainerRegistrations()
        {
            var program = new MyProgram(ConsoleLog.Instance);
            var result = await program.Run(new [] {"mycommand"});

            result.Should().Be(0);
        }

        class MyProgram : CalamariFlavourProgramAsync
        {
            public MyProgram(ILog log) : base(log)
            {

            }

            public new Task<int> Run(string[] args)
            {
                return base.Run(args);
            }
        }

        [Command("mycommand")]
        class MyCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield return resolver.Create<MyEmptyBehaviour>();
            }
        }

        class MyEmptyBehaviour : IDeployBehaviour
        {
            public bool IsEnabled(RunningDeployment context)
            {
                return false;
            }

            public Task Execute(RunningDeployment context)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}