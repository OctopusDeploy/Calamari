using System;

namespace Calamari.Integration.Scripting
{
    public class InvalidScriptException : Exception
    {
        public InvalidScriptException(string message) : base(message)
        {            
        }
    }
}