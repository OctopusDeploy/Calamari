using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.Shared
{
    public interface ILog
    {
        void Verbose(string message);
        void VerboseFormat(string message, params object[] args);
        void Info(string message);
        void InfoFormat(string message, params object[] args);
        void Warn(string message);
        void WarnFormat(string message, params object[] args);
        void Error(string message);
        void ErrorFormat(string message, params object[] args);
    }
}
