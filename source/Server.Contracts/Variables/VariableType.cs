using System;
using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Variables
{
    public class VariableType : CaseInsensitiveTypedString
    {
        public static readonly VariableType String = new VariableType("String");
        public static readonly VariableType Sensitive = new VariableType("Sensitive");
        public static readonly VariableType Certificate = new VariableType("Certificate");
        public static readonly VariableType WorkerPool = new VariableType("WorkerPool");

        //TODO: Move out to respective projects
        public static readonly VariableType AmazonWebServicesAccount = new VariableType("AmazonWebServicesAccount");
        public static readonly VariableType AzureAccount = new VariableType("AzureAccount");

        public VariableType(string value) : base(value)
        {
        }
    }
}