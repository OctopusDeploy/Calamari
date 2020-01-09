using System.Collections.Generic;

namespace Calamari.Integration.Processes
{
    public class CommandCaptureOutput : ICommandOutput
    {
        public List<string> Infos { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public void WriteInfo(string line)
        {
            Infos.Add(line);
        }

        public void WriteError(string line)
        {
            Errors.Add(line);
        }
    }
}