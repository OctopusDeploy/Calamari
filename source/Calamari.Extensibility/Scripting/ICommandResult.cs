using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.Extensibility.Scripting
{
    public interface ICommandResult
    {
        int ExitCode { get; }
        string Errors { get; }
    }
}
