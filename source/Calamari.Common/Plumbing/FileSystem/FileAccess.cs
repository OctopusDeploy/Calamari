using System;
using System.IO;
using System.Runtime.InteropServices;
using Calamari.Common.Plumbing.Logging;
using Polly;

namespace Calamari.Common.Plumbing.FileSystem
{
    public static class FileAccessChecker
    {
        public static T IsFileLocked<T>(Func<T> accessFile)
        {
                try
                {
                    Policy.Handle<IOException>()
                          .WaitAndRetry(
                                        3,
                                        i => TimeSpan.FromMilliseconds(200))
                          .Execute(accessFile);
                }
                catch (Exception e)
                {
                    var errorCode = Marshal.GetHRForException(e) & ((1 << 16) - 1);
                    var isLocked = errorCode == 32 || errorCode == 33;
                    Log.Error($"File is {isLocked}, threw with error code {errorCode}");
                    throw e;
                }

                return accessFile();
        }
        
        public static void IsFileLocked(Action accessFile)
        {
            try
            {
                accessFile();
            }
            catch (Exception e)
            {
                var errorCode = Marshal.GetHRForException(e) & ((1 << 16) - 1);
                var isLocked = errorCode == 32 || errorCode == 33;
                Log.Warn($"File is {isLocked}, threw with error code {errorCode}");
                Log.Warn(e.Message);
                throw e;
            }
        }
    }
}