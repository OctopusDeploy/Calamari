using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Calamari.Deployment;
using Calamari.Integration.Retry;
using Calamari.Util;
using System.Runtime.InteropServices;

namespace Calamari.Integration.FileSystem
{
    public abstract class CalamariPhysicalFileSystem : ICalamariFileSystem
    {
        public static CalamariPhysicalFileSystem GetPhysicalFileSystem()
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                return new NixCalamariPhysicalFileSystem();
            }

            return new WindowsPhysicalFileSystem();
        }

        protected IFile File { get; set; } = new StandardFile();
        protected IDirectory Directory { get; set; } = new StandardDirectory();

        /// <summary>
        /// For file operations, try again after 100ms and again every 200ms after that
        /// </summary>
        static readonly LimitedExponentialRetryInterval RetryIntervalForFileOperations = new LimitedExponentialRetryInterval(100, 200, 2);

        /// <summary>
        /// For file operations, retry constantly up to one minute
        /// </summary>
        /// <remarks>
        /// Windows services can hang on to files for ~30s after the service has stopped as background
        /// threads shutdown or are killed for not shutting down in a timely fashion
        /// </remarks>
        protected static RetryTracker GetRetryTracker()
        {
            return new RetryTracker(maxRetries:10000, 
                timeLimit: TimeSpan.FromMinutes(1), 
                retryInterval: RetryIntervalForFileOperations);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool DirectoryIsEmpty(string path)
        {
            try
            {
                return !Directory.GetFileSystemEntries(path).Any();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void DeleteFile(string path)
        {
            DeleteFile(path, FailureOptions.ThrowOnFailure);
        }

        public virtual void DeleteFile(string path, FailureOptions options)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var retry = GetRetryTracker();

            while (retry.Try())
            {
                try
                {
                    if (File.Exists(path))
                    {
                        if (retry.IsNotFirstAttempt)
                        {
                            File.SetAttributes(path, FileAttributes.Normal);
                        }
                        File.Delete(path);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.VerboseFormat("Retry #{0} on delete file '{1}'. Exception: {2}", retry.CurrentTry, path, ex.Message);
                        }
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        if (options == FailureOptions.ThrowOnFailure)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        public void DeleteDirectory(string path)
        {
            Directory.Delete(path, true);
        }

        public void DeleteDirectory(string path, FailureOptions options)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            var retry = GetRetryTracker();
            while (retry.Try())
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        var dir = new DirectoryInfo(path);
                        dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                        dir.Delete(true);
                        EnsureDirectoryDeleted(path, options);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.VerboseFormat("Retry #{0} on delete directory '{1}'. Exception: {2}", retry.CurrentTry, path, ex.Message);
                        }
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        if (options == FailureOptions.ThrowOnFailure)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        void EnsureDirectoryDeleted(string path, FailureOptions failureOptions)
        {
            var retry = GetRetryTracker(); 

            while (retry.Try())
            {
                if (!Directory.Exists(path))
                    return;

                if (retry.CanRetry() && retry.ShouldLogWarning())
                    Log.VerboseFormat("Waiting for directory '{0}' to be deleted", path);
            }

            var message = $"Directory '{path}' still exists, despite requested deletion";

            if (failureOptions == FailureOptions.ThrowOnFailure)
                throw new Exception(message);

            Log.Verbose(message);
        }

        public virtual IEnumerable<string> EnumerateFilesWithGlob(string parentDirectoryPath, params string[] globPattern)
        {
            return EnumerateWithGlob(parentDirectoryPath, globPattern).Select(fi => fi.FullName).Where(FileExists);
        }

        private IEnumerable<FileSystemInfo> EnumerateWithGlob(string parentDirectoryPath, params string[] globPattern)
        {
            var results = globPattern.Length == 0
                ? Glob.Expand(Path.Combine(parentDirectoryPath, "*"))
                : globPattern.SelectMany(pattern => Glob.Expand(Path.Combine(parentDirectoryPath, pattern)));

            return results
                .GroupBy(fi => fi.FullName) // use groupby + first to do .Distinct using fullname
                .Select(g => g.First());
        }

        public virtual IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            var parentDirectoryInfo = new DirectoryInfo(parentDirectoryPath);

            return searchPatterns.Length == 0
                ? parentDirectoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly).Select(fi => fi.FullName)
                : searchPatterns.SelectMany(pattern => parentDirectoryInfo.GetFiles(pattern, SearchOption.TopDirectoryOnly).Select(fi => fi.FullName));
        }

        public virtual IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns)
        {
            var parentDirectoryInfo = new DirectoryInfo(parentDirectoryPath);

            return searchPatterns.Length == 0
                ? parentDirectoryInfo.GetFiles("*", SearchOption.AllDirectories).Select(fi => fi.FullName)
                : searchPatterns.SelectMany(pattern => parentDirectoryInfo.GetFiles(pattern, SearchOption.AllDirectories).Select(fi => fi.FullName));
        }

        public IEnumerable<string> EnumerateDirectories(string parentDirectoryPath)
        {
            return Directory.EnumerateDirectories(parentDirectoryPath);
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath)
        {
            return Directory.EnumerateDirectoriesRecursively(parentDirectoryPath);
        }

        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        public string ReadFile(string path)
        {
            Encoding encoding;
            return ReadFile(path, out encoding);
        }

        //Read a file and detect different encodings. Based on answer from http://stackoverflow.com/questions/1025332/determine-a-strings-encoding-in-c-sharp
        //but don't try to handle UTF16 without BOM or non-default ANSI codepage.
        public string ReadFile(string filename, out Encoding encoding)
        {
            var b = File.ReadAllBytes(filename);

            // BOM/signature exists (sourced from http://www.unicode.org/faq/utf_bom.html#bom4)
            if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF) { encoding = Encoding.GetEncoding("utf-32BE"); return Encoding.GetEncoding("utf-32BE").GetString(b, 4, b.Length - 4); }  // UTF-32, big-endian 
            else if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00) { encoding = Encoding.UTF32; return Encoding.UTF32.GetString(b, 4, b.Length - 4); }    // UTF-32, little-endian
            else if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) { encoding = Encoding.BigEndianUnicode; return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2); }     // UTF-16, big-endian
            else if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) { encoding = Encoding.Unicode; return Encoding.Unicode.GetString(b, 2, b.Length - 2); }            // UTF-16, little-endian
            else if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { encoding = Encoding.UTF8; return Encoding.UTF8.GetString(b, 3, b.Length - 3); }  // UTF-8
            else if (b.Length >= 3 && b[0] == 0x2b && b[1] == 0x2f && b[2] == 0x76) { encoding = Encoding.UTF7; return Encoding.UTF7.GetString(b, 3, b.Length - 3);  } // UTF-7

            // Some text files are encoded in UTF8, but have no BOM/signature. Hence
            // the below manually checks for a UTF8 pattern. This code is based off
            // the top answer at: http://stackoverflow.com/questions/6555015/check-for-invalid-utf8
            var i = 0;
            var utf8 = false;
            var ascii = true;
            while (i < b.Length - 4)
            {
                if (b[i] <= 0x7F) { i += 1; continue; }     // If all characters are below 0x80, then it is valid UTF8, but UTF8 is not 'required' 
                if (b[i] >= 0xC2 && b[i] <= 0xDF && b[i + 1] >= 0x80 && b[i + 1] < 0xC0) { i += 2; utf8 = true; ascii = false; continue; }
                if (b[i] >= 0xE0 && b[i] <= 0xF0 && b[i + 1] >= 0x80 && b[i + 1] < 0xC0 && b[i + 2] >= 0x80 && b[i + 2] < 0xC0) { i += 3; utf8 = true; ascii = false; continue; }
                if (b[i] >= 0xF0 && b[i] <= 0xF4 && b[i + 1] >= 0x80 && b[i + 1] < 0xC0 && b[i + 2] >= 0x80 && b[i + 2] < 0xC0 && b[i + 3] >= 0x80 && b[i + 3] < 0xC0) { i += 4; utf8 = true; ascii = false; continue; }
                ascii = false; utf8 = false; break;
            }
            if (ascii)
            {
                encoding = Encoding.ASCII;
                return Encoding.ASCII.GetString(b);
            }
            if (utf8)
            {
                encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); //UTF8 with no BOM
                return Encoding.UTF8.GetString(b);
            }
            // If all else fails, the encoding is probably (though certainly not definitely) the user's local codepage! 
            // this probably something like Windows 1252 on Windows, but is Encoding.Default is UTF8 on Linux so this probably isn't right in Linux.
            encoding = Encoding.Default;

            return encoding.GetString(b);
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void OverwriteFile(string path, string contents)
        {
            RetryTrackerFileAction(() => WriteAllText(path, contents), path, "overwrite");
        }

        public void OverwriteFile(string path, string contents, Encoding encoding)
        {
            RetryTrackerFileAction(() => WriteAllText(path, contents, encoding), path, "overwrite");
        }

        //File.WriteAllText won't overwrite a hidden file, so implement our own.
        private static void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            if (path.Length <= 0) throw new ArgumentException(path);

            //FileMode.Open causes an existing file to be truncated to the
            //length of our new data, but can't overwrite a hidden file,
            //so use FileMode.OpenOrCreate and set the new file length manually.
            using (var fs = new FileStream(path, FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(fs, encoding))
            {
                sw.Write(contents);
                sw.Flush();
                fs.SetLength(fs.Position);
            }
        }

        private static void WriteAllText(string path, string contents)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length <= 0) throw new ArgumentException(path);
            using (var fs = new FileStream(path, FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(contents);
                sw.Flush();
                fs.SetLength(fs.Position);
            }
        }

        public Stream OpenFile(string path, FileAccess access, FileShare share)
        {
            return OpenFile(path, FileMode.OpenOrCreate, access, share);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(path, mode, access, share);
        }

        public Stream CreateTemporaryFile(string extension, out string path)
        {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            path = Path.Combine(GetTempBasePath(), Guid.NewGuid() + extension);

            return OpenFile(path, FileAccess.ReadWrite, FileShare.Read);
        }

        string GetTempBasePath()
        {
            var path1 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path1 = Path.Combine(path1, Assembly.GetEntryAssembly()?.GetName().Name ?? "Octopus.Calamari");
            path1 = Path.Combine(path1, "Temp");
            var path = path1;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options)
        {
            PurgeDirectory(targetDirectory, fi => false, options);
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options, CancellationToken cancel)
        {
            PurgeDirectory(targetDirectory, fi => false, options, cancel);
        }

        public void PurgeDirectory(string targetDirectory, Predicate<IFileSystemInfo> exclude, FailureOptions options)
        {
            PurgeDirectory(targetDirectory, exclude, options, CancellationToken.None);
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options, params string[] globs)
        {
            Predicate<IFileSystemInfo> check = null;
            if (globs.Any())
            {
                var keep = EnumerateWithGlob(targetDirectory, globs);
                check = fsi =>
                {
                    return keep.Any(k => k is DirectoryInfo && fsi.FullName.IsChildOf(k.FullName) ||
                                         k.FullName == fsi.FullName);
                };
            }
            PurgeDirectory(targetDirectory, check, options, CancellationToken.None);
        }

        void PurgeDirectory(string targetDirectory, Predicate<IFileSystemInfo> exclude, FailureOptions options, CancellationToken cancel, bool includeTarget = false)
        {
            exclude = exclude?? (fi => false);

            if (!DirectoryExists(targetDirectory))
            {
                return;
            }

            foreach (var file in EnumerateFiles(targetDirectory))
            {
                cancel.ThrowIfCancellationRequested();

                var includeInfo = new FileSystemInfoAdapter(new FileInfo(file));
                if (exclude(includeInfo))
                {
                    continue;
                }

                DeleteFile(file, options);
            }

            foreach (var directory in EnumerateDirectories(targetDirectory))
            {
                cancel.ThrowIfCancellationRequested();

                var info = new DirectoryInfo(directory);
                var includeInfo = new FileSystemInfoAdapter(info);
                if (exclude(includeInfo))
                {
                    continue;
                }

                if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Directory.Delete(directory, false);
                }
                else
                {
                    PurgeDirectory(directory, exclude, options, cancel, includeTarget: true);
                }
            }

            if (includeTarget && DirectoryIsEmpty(targetDirectory))
                DeleteDirectory(targetDirectory, options);
        }

        public void OverwriteAndDelete(string originalFile, string temporaryReplacement)
        {
            var backup = originalFile + ".backup" + Guid.NewGuid();

            if (!File.Exists(originalFile))
                File.Copy(temporaryReplacement, originalFile, true);
            else
            {
                System.IO.File.Replace(temporaryReplacement, originalFile, backup);
            }

            File.Delete(temporaryReplacement);
            if (File.Exists(backup))
                File.Delete(backup);
        }

        public void WriteAllBytes(string filePath, byte[] data)
        {
            File.WriteAllBytes(filePath, data);
        }

        public string RemoveInvalidFileNameChars(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var invalidPathChars = Path.GetInvalidPathChars();
            var invalidFileChars = Path.GetInvalidFileNameChars();

            var result = new StringBuilder(path.Length);
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (!invalidPathChars.Contains(c) && !invalidFileChars.Contains(c))
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        public void MoveFile(string sourceFile, string destinationFile)
        {
            File.Move(sourceFile, destinationFile);
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        // ReSharper disable AssignNullToNotNullAttribute

        public int CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            return CopyDirectory(sourceDirectory, targetDirectory, CancellationToken.None);
        }

        public int CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel)
        {
            if (!DirectoryExists(sourceDirectory))
                return 0;

            if (!DirectoryExists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            int count = 0;
            var files = Directory.GetFiles(sourceDirectory, "*");
            foreach (var sourceFile in files)
            {
                cancel.ThrowIfCancellationRequested();

                var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                CopyFile(sourceFile, targetFile);
                count++;
            }

            foreach (var childSourceDirectory in Directory.GetDirectories(sourceDirectory))
            {
                var name = Path.GetFileName(childSourceDirectory);
                var childTargetDirectory = Path.Combine(targetDirectory, name);
                count += CopyDirectory(childSourceDirectory, childTargetDirectory, cancel);
            }

            return count;
        }

        public void CopyFile(string sourceFile, string targetFile)
        {
            RetryTrackerFileAction(() => File.Copy(sourceFile, targetFile, true), targetFile, "copy");
        }

        private static void RetryTrackerFileAction(Action fileAction, string target, string action)
        {
            var retry = GetRetryTracker();
            while (retry.Try())
            {
                try
                {
                    fileAction();
                    return;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.VerboseFormat("Retry #{0} on {1} '{2}'. Exception: {3}", retry.CurrentTry, action, target, ex.Message);
                        }
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public bool SkipFreeDiskSpaceCheck { get; set; }
        public int? FreeDiskSpaceOverrideInMegaBytes { get; set; }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            if (SkipFreeDiskSpaceCheck)
            {
                Log.Verbose($"{SpecialVariables.SkipFreeDiskSpaceCheck} is enabled. The check to ensure that the drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' has enough free space will be skipped.");
                return;
            }

            long? freeDiskSpaceOverrideInBytes = null;
            if (FreeDiskSpaceOverrideInMegaBytes.HasValue)
            {
                freeDiskSpaceOverrideInBytes = ((long) FreeDiskSpaceOverrideInMegaBytes*1024*1024);
                Log.Verbose($"{SpecialVariables.FreeDiskSpaceOverrideInMegaBytes} has been specified. We will check and ensure that the drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' has {((ulong)freeDiskSpaceOverrideInBytes).ToFileSizeString()} free disk space.");
            }

            EnsureDiskHasEnoughFreeSpace(directoryPath, freeDiskSpaceOverrideInBytes ?? 500L * 1024 * 1024);
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            ulong totalNumberOfFreeBytes;

            var success = GetDiskFreeSpace(directoryPath, out totalNumberOfFreeBytes);
            if (!success)
                return;

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // If a free disk space override value has not been provided, always make sure at least 500MB are available regardless of what we need
            if(!FreeDiskSpaceOverrideInMegaBytes.HasValue)
                required = Math.Max(required, 500L * 1024 * 1024);

            if (totalNumberOfFreeBytes < required)
            {
                throw new IOException(
                    $"The drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' does not have enough free disk space available for this operation to proceed. The disk only has {totalNumberOfFreeBytes.ToFileSizeString()} available; please free up at least {required.ToFileSizeString()}.");
            }
        }

        public string GetFullPath(string relativeOrAbsoluteFilePath)
        {
            if (!Path.IsPathRooted(relativeOrAbsoluteFilePath))
            {
                relativeOrAbsoluteFilePath = Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsoluteFilePath);
            }

            relativeOrAbsoluteFilePath = Path.GetFullPath(relativeOrAbsoluteFilePath);
            return relativeOrAbsoluteFilePath;
        }

        protected abstract bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes);

        public string GetRelativePath(string fromFile, string toFile)
        {
            var fromPathTokens = fromFile.Split(Path.DirectorySeparatorChar);
            var toPathTokens = toFile.Split(Path.DirectorySeparatorChar);

            var matchingTokens = 0;
            for (; matchingTokens < fromPathTokens.Count() - 1; matchingTokens++)
            {
                if (!fromPathTokens[matchingTokens].Equals(toPathTokens[matchingTokens], StringComparison.Ordinal))
                    break;
            }

            var relativePath = new StringBuilder();
            for (var i = matchingTokens; i < fromPathTokens.Length - 1; i++)
                relativePath.Append("..").Append(Path.DirectorySeparatorChar);

            for (var i = matchingTokens; i < toPathTokens.Length; i++)
            {
                relativePath.Append(toPathTokens[i]);
                if (i != toPathTokens.Length - 1)
                    relativePath.Append(Path.DirectorySeparatorChar);
            }

            return relativePath.ToString();
        }

        public DateTime GetCreationTime(string filePath)
        {
            return File.GetCreationTime(filePath);
        }

        public string GetFileName(string filePath)
        {
            return new FileInfo(filePath).Name;
        }

        public Stream OpenFileExclusively(string filePath, FileMode fileMode, FileAccess fileAccess)
        {
            return File.Open(filePath, fileMode, fileAccess, FileShare.None);
        }
    }
}
