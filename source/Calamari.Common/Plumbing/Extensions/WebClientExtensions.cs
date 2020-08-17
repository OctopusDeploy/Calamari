using System;
using System.Net;
using System.Threading;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class WebClientExtensions
    {
        public delegate void ProgressEventHandler(int progressPercentage, long totalBytes);

        public static void DownloadFileWithProgress(this WebClient client, string uri, string fileName, ProgressEventHandler progressHandler)
        {
            var lastCall = DateTime.Now;
            var throttle = TimeSpan.FromSeconds(3).Ticks;
            var hasCalled = false;

            client.DownloadProgressChanged += (sender, args) =>
            {
                if (DateTime.Now.Ticks - lastCall.Ticks <= throttle && !(hasCalled && args.ProgressPercentage == 100))
                    return;
                progressHandler(args.ProgressPercentage, args.TotalBytesToReceive);
                hasCalled = true;
                lastCall = DateTime.Now;
            };

            client.DownloadFileCompleted += (sender, args) =>
            {
                lock (args.UserState)
                {
                    Monitor.Pulse(args.UserState);
                }
            };

            var syncObject = new object();
            lock (syncObject)
            {
                client.DownloadFileAsync(new Uri(uri), fileName, syncObject);
                Monitor.Wait(syncObject);
            }
        }
    }
}