using System;
using Octopus.TinyTypes;

namespace Sashimi.Server.Contracts.Actions.Templates
{
    public class ControlType : CaseInsensitiveStringTinyType
    {
        public static readonly ControlType SingleLineText = new("SingleLineText");
        public static readonly ControlType MultiLineText = new("MultiLineText");
        public static readonly ControlType Select = new("Select");
        public static readonly ControlType Checkbox = new("Checkbox");
        public static readonly ControlType Sensitive = new("Sensitive");
        public static readonly ControlType StepName = new("StepName");
        public static readonly ControlType Certificate = new("Certificate");
        public static readonly ControlType WorkerPool = new("WorkerPool");

        public ControlType(string value) : base(value)
        {
        }
    }
}