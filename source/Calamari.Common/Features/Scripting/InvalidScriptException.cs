using System;

namespace Calamari.Common.Features.Scripting
{
    public class InvalidScriptException : Exception
    {
        public InvalidScriptException(string message)
            : base(message)
        {
        }
    }
}