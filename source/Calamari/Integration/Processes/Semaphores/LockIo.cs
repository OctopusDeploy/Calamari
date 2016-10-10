using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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
                            return lockContent;
                        }
                        return new OtherProcessOwnsFileLock(lockContent);
                    }
                    return new MissingFileLock();
                }
            }
            catch (FileNotFoundException)
            {
                return new MissingFileLock();
            }
            catch (IOException)
            {
                return new OtherProcessHasExclusiveLockOnFileLock();
            }
            catch (SerializationException)
            {
                return new UnableToDeserialiseLockFile(fileSystem.GetCreationTime(lockFilePath));
            }
            catch (Exception) //We have no idea what went wrong - reacquire this lock
            {
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
                            return true;
                        fileMode = FileMode.Create;
                    }
                    else if (currentContent.GetType() == typeof(UnableToDeserialiseLockFile))
                    {
                        DeleteLock(lockFilePath);
                    }
                }
                using (var stream = fileSystem.OpenFileExclusively(lockFilePath, fileMode, FileAccess.Write))
                {
                    jsonSerializer.WriteObject(stream, fileLock);
                }
                var writtenContent = ReadLock(lockFilePath);
                return Equals(writtenContent, fileLock);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void DeleteLock(string lockFilePath)
        {
            try
            {
                fileSystem.DeleteFile(lockFilePath);
            }
            catch (Exception)
            {
                // ignored - handled in create
            }
        }
    }
}