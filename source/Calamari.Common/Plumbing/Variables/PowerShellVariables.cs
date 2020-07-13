using System;

namespace Calamari.Common.Plumbing.Variables
{
    public class PowerShellVariables
    {
        public static readonly string CustomPowerShellVersion = "Octopus.Action.PowerShell.CustomPowerShellVersion";
        public static readonly string ExecuteWithoutProfile = "Octopus.Action.PowerShell.ExecuteWithoutProfile";
        public static readonly string DebugMode = "Octopus.Action.PowerShell.DebugMode";
        public static readonly string UserName = "Octopus.Action.PowerShell.UserName";
        public static readonly string Password = "Octopus.Action.PowerShell.Password";
        public static readonly string Edition = "Octopus.Action.PowerShell.Edition";

        public static class PSDebug
        {
            public static readonly string Trace = "Octopus.Action.PowerShell.PSDebug.Trace";
            public static readonly string Strict = "Octopus.Action.PowerShell.PSDebug.Strict";
        }
    }
}