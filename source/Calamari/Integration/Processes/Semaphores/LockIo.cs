using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.Processes.Semaphores
{
    internal class LockIo : ILockIo
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly DataContractJsonSerializer jsonSerializer;

        internal LockIo(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            jsonSerializer = new DataContractJsonSerializer(typeof(FileLock), new[] { typeof(FileLock) }, int.MaxValue, true, null, true);
        }

        public string GetFilePath(string lockName)
        {
            return Path.Combine(Path.GetTempPath(), lockName + ".lck");
        }

        public bool LockExists(string lockFilePath)
        {
            return fileSystem.FileExists(lockFilePath);
        }

        public FileLock ReadLock(string lockFilePath)
        {
            try
            {
                using (var stream = fileSystem.OpenFileExclusively(lockFilePath, FileMode.Open, FileAccess.Read))
                {
                    var obj = jsonSerializer.ReadObject(stream);
                    if ((FileLock)obj != null)
                    {
                        var lockContent = (FileLock) obj;
                        if (lockContent.BelongsToCurrentProcessAndThread())
                        {
                            Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Reading lock {lockFilePath} - it belongs to me");
                            return lockContent;
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
                return new UnableToDeserialiseLockFile(fileSystem.GetCreationTime(lockFilePath));
            }
            catch (Exception ex) //We have no idea what went wrong - reacquire this lock
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got unknown exception: {ex}");
                return new OtherProcessHasExclusiveLockOnFileLock();
            }
        }

        public bool WriteLock(string lockFilePath, FileLock fileLock)
        {
            try
            {
                var fileMode = FileMode.CreateNew;
                if (LockExists(lockFilePath))
                {
                    var currentContent = ReadLock(lockFilePath);
                    if (Equals(currentContent, fileLock))
                    {
                        if (currentContent.Timestamp == fileLock.Timestamp)
                        {
                            Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Attempted to write lock but we already owned it");
                            return true;
                        }
                        Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - We already owned the lock - updating the timestamp");
                        fileMode = FileMode.Create;
                    }
                    else if (currentContent.GetType() == typeof(UnableToDeserialiseLockFile))
                    {
                        DeleteLock(lockFilePath);
                    }
                }
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Writing lock {lockFilePath}");
                using (var stream = fileSystem.OpenFileExclusively(lockFilePath, fileMode, FileAccess.Write))
                {
                    jsonSerializer.WriteObject(stream, fileLock);
                }
                var writtenContent = ReadLock(lockFilePath);
                return Equals(writtenContent, fileLock);
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

        public void DeleteLock(string lockFilePath)
        {
            try
            {
                fileSystem.DeleteFile(lockFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got unknown exception while deleting: {ex}");
            }
        }
    }
}