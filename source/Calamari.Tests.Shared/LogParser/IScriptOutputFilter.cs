using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Tests.Shared.LogParser
{
    public interface IScriptOutputFilter
    {
        TestOutputVariableCollection TestOutputVariables { get; }
        List<TestScriptOutputAction> Actions { get; }
        List<ServiceMessage> ServiceMessages { get; }
        List<CollectedArtifact> Artifacts { get; }
        string? ResultMessage { get; }

        void Write(ProcessOutputSource source, string text);
    }
}