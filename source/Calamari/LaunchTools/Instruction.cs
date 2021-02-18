using System;
using Newtonsoft.Json.Linq;

namespace Calamari.LaunchTools
{
    public class Instruction
    {
        public LaunchTools Launcher { get; set; }
        public JToken LauncherInstructions { get; set; }
        public string LauncherInstructionsRaw => LauncherInstructions.ToString();
    }
}