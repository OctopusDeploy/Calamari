using System;

namespace Calamari.Integration.Processes
{
    public class ConsoleCommandOutput : ICommandOutput
    {
        public void WriteInfo(string line)
        {
            Console.WriteLine(line);
        }

        public void WriteError(string line)
        {
            Console.Error.WriteLine(line);
        }
    }
}