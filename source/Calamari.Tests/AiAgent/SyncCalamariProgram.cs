using System.Reflection;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.Tests;

// Calamari.Program's constructor is protected (Main is the only intended entry point), so
// CommandTestBuilder - which constructs it via reflection - needs a public wrapper to get at it.
// Everything else (ConfigureContainer, pipeline-command dispatch) is inherited unchanged, so
// RunAgentCommand is exercised through the exact same production container as the real Calamari.exe.
class SyncCalamariProgram : Calamari.Program
{
    public SyncCalamariProgram(ILog log) : base(log)
    {
    }

    // The base implementation returns GetType().Assembly, which would resolve to this test
    // wrapper's own assembly (Calamari.Tests) rather than Calamari, where RunAgentCommand lives.
    protected override Assembly GetProgramAssemblyToRegister() => typeof(Calamari.Program).Assembly;
}
