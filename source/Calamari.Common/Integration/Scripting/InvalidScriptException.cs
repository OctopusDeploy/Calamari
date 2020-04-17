using System;

namespace Calamari.Common.Integration.Scripting
{
    public class InvalidScriptException : Exception
    {
        public InvalidScriptException(string message) : base(message)
        {            
        }
    }
}