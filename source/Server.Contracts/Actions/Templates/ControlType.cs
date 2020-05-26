using System.Collections.Generic;
using Octopus.TinyTypes;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Actions.Templates
{
    public class ControlType : CaseInsensitiveTypedString
    {
        public static readonly ControlType SingleLineText = new ControlType("SingleLineText");
        public static readonly ControlType MultiLineText = new ControlType("MultiLineText");
        public static readonly ControlType Select = new ControlType("Select");
        public static readonly ControlType Checkbox = new ControlType("Checkbox");
        public static readonly ControlType Sensitive = new ControlType("Sensitive");
        public static readonly ControlType StepName = new ControlType("StepName");
        public static readonly ControlType Certificate = new ControlType("Certificate");
        public static readonly ControlType WorkerPool = new ControlType("WorkerPool");

        //TODO: Move out to respective projects
        public static readonly ControlType AmazonWebServicesAccount = new ControlType("AmazonWebServicesAccount");
        public static readonly ControlType AzureAccount = new ControlType("AzureAccount");

        public ControlType(string value) : base(value)
        {
        }
    }
}