using System;
using System.IO;
using Autofac;
using Calamari.Commands.Support;

namespace Calamari.Tests.Helpers
{
    class TestProgram : Program
    {
        public TestProgram(InMemoryLog log) : base(log)
        {
            Log = log;
        }

        public InMemoryLog Log { get; }
        ICommand CommandOverride { get; set; }
        public bool StubWasCalled { get; set; }
        public IVariables VariablesOverride { get; set; }
        
        public int Run(string[] args)
        {
            var options = CommonOptions.Parse(args);
            return Run(options);
        }
        
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
                builder.RegisterInstance(CommandOverride).Named<ICommand>("stub");
            if (VariablesOverride != null)
                builder.RegisterInstance(VariablesOverride).As<IVariables>();
            return builder;
        }
    }

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