using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers
{
    class TestProgram : Program
    {
        public TestProgram(InMemoryLog log) : base(log)
        {
            TestLog = log;
        }

        internal InMemoryLog TestLog { get; }
        ICommandWithArgs CommandOverride { get; set; }
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

        protected override Assembly GetProgramAssemblyToRegister()
        {
            // Return Calamari Assembly as it contains the helm commands that needs to be tested.
            // Don't register Calamari.Test Assembly as the only thing we need to register is the stub command and that's handled below
            return typeof(Program).Assembly;
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            // Register CommandOverride so it shows up first in IEnumerable
            if (CommandOverride != null)
                builder.RegisterInstance(CommandOverride).WithMetadata("Name", "stub").As<ICommandWithArgs>();

            base.ConfigureContainer(builder, options);

            // Register after base so Singleton gets overridden
            if (VariablesOverride != null)
                builder.RegisterInstance(VariablesOverride).As<IVariables>();
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