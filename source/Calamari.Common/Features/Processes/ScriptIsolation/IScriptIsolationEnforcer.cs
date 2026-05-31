using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public interface IScriptIsolationEnforcer
{
    ILockHandle Enforce(CommonOptions.ScriptIsolationOptions scriptIsolationOptions);

    Task<ILockHandle> EnforceAsync(
        CommonOptions.ScriptIsolationOptions scriptIsolationOptions,
        CancellationToken cancellationToken
    );
}
