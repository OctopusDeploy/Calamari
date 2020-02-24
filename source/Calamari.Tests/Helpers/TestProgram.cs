using System;
using System.IO;
using Autofac;
using Calamari.Commands.Support;
using Octostache;

namespace Calamari.Tests.Helpers
{
    class TestProgram : Program
    {
        public InMemoryLog Log { get; } = new InMemoryLog();
        public ICommand CommandOverride { get; set; }
        public bool StubWasCalled { get; set; }
        public IVariables VariablesOverride { get; set; }

        public int RunStubCommand()
        {
            CommandOverride  = new StubCommand(() => StubWasCalled = true);
            var commonOptions = new CommonOptions("stub");
            return Run(commonOptions);
        }
        
        protected override ContainerBuilder BuildContainer(CommonOptions options)
        {
            var builder = base.BuildContainer(options);
            builder.RegisterInstance(Log).As<ILog>();
            if (CommandOverride != null)
                builder.RegisterInstance(CommandOverride).As<ICommand>();
            if (VariablesOverride != null)
                builder.RegisterInstance(VariablesOverride).As<IVariables>();
            return builder;
        }
    }

    [Command("stub")]
    class StubCommand : ICommand
    {
        readonly Action callback;

        public StubCommand(Action callback)
        {
            this.callback = callback;
        }

        public void GetHelp(TextWriter writer)
        {
            throw new NotImplementedException();
        }

        public int Execute(string[] commandLineArguments)
        {
            callback();
            return 0;
        }
    }
}