using System;

namespace Calamari.Common.Commands
{
    /// <summary>
    /// We defer logs from startup, until we know what command we're going to run
    /// For classes that do not implement this interface, any deferred logs will be flushed before Execute is called
    /// Classes that implement this interface are responsible for flushing deferred logs themselves
    /// </summary>
    public interface IWantCustomHandlingOfDeferredLogs
    {
    }
}
