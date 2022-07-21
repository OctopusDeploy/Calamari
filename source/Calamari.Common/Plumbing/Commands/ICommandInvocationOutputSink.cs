using System;

namespace Calamari.Common.Plumbing.Commands
{
    public interface ICommandInvocationOutputSink
    {
        void WriteInfo(string line);
        void WriteError(string line);
    }
}