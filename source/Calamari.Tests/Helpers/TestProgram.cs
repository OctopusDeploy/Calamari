using System;
using System.IO;
using Autofac;
using Calamari.Commands.Support;
using Octostache;

namespace Calamari.Tests.Helpers
{
    class TestProgram : Program
    {
        public TestProgram(InMemoryLog log) : base(log)
        {
            Log = log;
        }

        public InMemoryLog Log { get; }
        public ICommandWithArgs CommandOverride { get; set; }
        public bool StubWasCalled { get; set; }
        public IVariables VariablesOverride { get; set; }

        public int RunWithArgs(string[] args)
        {
            return Run(args);
        }

        public int RunStubCommand()
        {
            CommandOverride  = new StubCommand(() => StubWasCalled = true);
            return Run(new [] {"stub"});
        }
        
        protected override ContainerBuilder BuildContainer(CommonOptions options)
        {
            var builder = base.BuildContainer(options);
            builder.RegisterInstance(Log).As<ILog>();
            if (CommandOverride != null)
                builder.RegisterInstance(CommandOverride).As<ICommandWithArgs>();
            if (VariablesOverride != null)
                builder.RegisterInstance(VariablesOverride).As<IVariables>();
            return builder;
        }
    }

    [Command("stub")]
    class StubCommand : ICommandWithArgs
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