using System;
using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Variables
{
    public class VariableType : CaseInsensitiveStringTinyType
    {
        public static readonly VariableType String = new("String");
        public static readonly VariableType Sensitive = new("Sensitive");
        public static readonly VariableType Certificate = new("Certificate");
        public static readonly VariableType WorkerPool = new("WorkerPool");

        public VariableType(string value) : base(value)
        {
        }
    }
}