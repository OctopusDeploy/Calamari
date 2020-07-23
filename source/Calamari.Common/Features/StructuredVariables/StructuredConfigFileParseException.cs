using System;

namespace Calamari.Common.Features.StructuredVariables
{
    public class StructuredConfigFileParseException : Exception
    {
        public StructuredConfigFileParseException(string message, Exception e) : base(message, e)
        {
            
        }
    }
}