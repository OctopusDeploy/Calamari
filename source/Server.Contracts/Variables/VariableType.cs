using System;
using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Variables
{
    public class VariableType : CaseInsensitiveTypedString
    {
        public static VariableType String => new VariableType("String");
        public static VariableType Sensitive => new VariableType("Sensitive");
        public static VariableType Certificate => new VariableType("Certificate");
        public static VariableType WorkerPool => new VariableType("WorkerPool");

        //TODO: Move out to respective projects
        public static VariableType AmazonWebServicesAccount => new VariableType("AmazonWebServicesAccount");
        public static VariableType AzureAccount => new VariableType("AzureAccount");

        public VariableType(string value) : base(value)
        {
        }
    }
}