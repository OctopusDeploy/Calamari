using System;
using System.IO;
using System.Threading;

namespace Calamari.Integration.FileSystem
{
    public class TemporaryFile : IDisposable
    {
        private readonly string filePath;

        public TemporaryFile(string filePath)
        {
            this.filePath = filePath;
        }

        public string FilePath
        {
            get { return filePath; }
        }

        public void Dispose()
        {
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
