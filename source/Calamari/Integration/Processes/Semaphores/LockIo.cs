using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace Calamari.Integration.Processes.Semaphores
{
    internal static class LockIo
    {
        private static readonly DataContractJsonSerializer JsonSerializer;

        static LockIo()
        {
            JsonSerializer = new DataContractJsonSerializer(typeof(FileLockContent), new[] { typeof(FileLockContent) }, int.MaxValue, true, null, true);
        }

        public static string GetFilePath(string lockName)
        {
            return Path.Combine(Path.GetTempPath(), lockName + ".lck");
        }

        public static bool LockExists(string lockFilePath)
        {
            return File.Exists(lockFilePath);
        }

        public static FileLockContent ReadLock(string lockFilePath)
        {
            try
            {
                using (var stream = File.Open(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var obj = JsonSerializer.ReadObject(stream);
                    if ((FileLockContent)obj != null)
                    {
                        var lockContent = (FileLockContent) obj;
                        if (lockContent.ProcessId == Process.GetCurrentProcess().Id)
                        {
                            if (lockContent.ThreadId == Thread.CurrentThread.ManagedThreadId)
                            {
                                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Reading lock {lockFilePath} - it belongs to me");
                                return lockContent;
                            }
                        }
                        Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Reading lock {lockFilePath} - it belongs to someone else ({lockContent.ProcessId}/{lockContent.ThreadId}).");
                        return new OtherProcessOwnsFileLock(lockContent);
                    }
                    return new MissingFileLock();
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got FileNotFoundException");
                return new MissingFileLock();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got IOException: {ex.Message}");
                return new OtherProcessHasExclusiveLockOnFileLock();
            }
            catch (SerializationException ex)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got SerializationException: {ex.Message}");
                return new UnableToDeserialiseLockFile(File.GetCreationTime(lockFilePath));
            }
            catch (Exception ex) //We have no idea what went wrong - reacquire this lock
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got unknown exception: {ex}");
                return new OtherProcessHasExclusiveLockOnFileLock();
            }
        }

        public static bool WriteLock(string lockFilePath, FileLockContent lockContent)
        {
            try
            {
                if (LockExists(lockFilePath))
                {
                    var currentContent = ReadLock(lockFilePath);
                    if (Equals(currentContent, lockContent))
                    {
                        if (currentContent.Timestamp != lockContent.Timestamp)
                        {
                            Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - We already owned the lock - updating the timestamp");
                            using (var stream = File.Open(lockFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                JsonSerializer.WriteObject(stream, lockContent);
                            }
                            return true;
                        }
                        Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Attempted to write lock but we already owned it");
                        return true;
                    }
                    if (currentContent.GetType() == typeof(UnableToDeserialiseLockFile))
                    {
                        DeleteLock(lockFilePath);
                    }
                }
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Writing lock {lockFilePath}");
                using (var stream = File.Open(lockFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.WriteObject(stream, lockContent);
                }
                var writtenContent = ReadLock(lockFilePath);
                return Equals(writtenContent, lockContent);
            }
            catch (IOException)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got IOException while writing lock file");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got unknown exception while writing lock file: {ex}");

                return false;
            }
        }

        public static void DeleteLock(string lockFilePath)
        {
            try
            {
                File.Delete(lockFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got unknown exception while deleting: {ex}");
            }
        }
    }
}