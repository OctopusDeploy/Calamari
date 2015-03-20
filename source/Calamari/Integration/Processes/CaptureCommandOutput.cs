using System.Collections.Generic;

namespace Calamari.Integration.Processes
{
    public class CaptureCommandOutput : ICommandOutput
    {
        private readonly List<string> infos = new List<string>();
        private readonly List<string> errors = new List<string>();

        public void WriteInfo(string line)
        {
            infos.Add(line);
        }

        public void WriteError(string line)
        {
            errors.Add(line);
        }

        public IList<string> Infos
        {
            get { return infos; }
        }

        public IList<string> Errors
        {
            get { return errors; }
        }
    }
}