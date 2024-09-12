using System;
using Calamari.FullFrameworkTools.Contracts;

namespace Calamari.Tests.Helpers
{
    class NullFullFrameworkLog : ILog
    {
        public void Verbose(string message)
        {
            /*throw new NotImplementedException();*/
        }

        public void Info(string message)
        {
            /*throw new NotImplementedException();*/
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
        
        }
    }
}