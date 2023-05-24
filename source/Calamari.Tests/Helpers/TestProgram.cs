using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers
{
    class TestProgram : Program
    {
        private bool registerTestAssembly = true;

        public TestProgram(ILog log = null) : base(log = log ?? new InMemoryLog())
        {
            TestLog = log as InMemoryLog;
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
            registerTestAssembly = false;
            CommandOverride  = new StubCommand(() => StubWasCalled = true);
            return Run(new [] {"stub"});
        }

        protected override Assembly GetProgramAssemblyToRegister()
        {
            return typeof(Program).Assembly;
        }

        protected override IEnumerable<Assembly> GetAllAssembliesToRegister()
        {
            var allAssemblies = base.GetAllAssembliesToRegister();
            if (registerTestAssembly)
            {
                allAssemblies = allAssemblies.Concat(new[] { typeof(TestProgram).Assembly });
            }

            return allAssemblies;
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