using System;

namespace Calamari.Shared.Scripting
{
    public class InvalidScriptException : Exception
    {
        public InvalidScriptException(string message) : base(message)
        {            
        }
    }
}