using System;

namespace Calamari.Common.Features.StructuredVariables
{
    public class StructuredConfigFileParseException : Exception
    {
        public StructuredConfigFileParseException(string message, Exception innerException) : base(message, innerException)
        {
            
        }
    }
}