using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Util;

namespace Calamari.AiAgent.Tests;

// Test-only async flavour host for exercising RunAgentCommand via CommandTestBuilder,
// now that it lives in the Calamari assembly instead of its own flavour executable.
// Registers only the AiAgent types it needs directly, rather than scanning the whole
// Calamari assembly (which would sweep in unrelated production wrappers/behaviours,
// e.g. Kubernetes' ManifestReportScriptWrapper, that this harness can't satisfy).
class Program : CalamariFlavourProgramAsync
{
    public Program(ILog log) : base(log)
    {
    }

    protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
    {
        base.ConfigureContainer(builder, options);

        builder.RegisterType<ClaudeSettingsWriter>();
        builder.RegisterType<InvokeClaudeCodeBehaviour>().AsSelf().InstancePerDependency();
        builder.RegisterType<RunAgentCommand>().Named<PipelineCommand>(typeof(RunAgentCommand).GetCommandNameFromAttribute());
    }

    protected override IEnumerable<Assembly> GetProgramAssembliesToRegister()
    {
        yield break;
    }
}
