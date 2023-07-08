using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem.GlobExpressions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Retry;

namespace Calamari.Common.Plumbing.FileSystem
{
    public abstract class CalamariPhysicalFileSystem : ICalamariFileSystem
    {
        /// <summary>
        /// For file operations, try again after 100ms and again every 200ms after that
        /// </summary>
        static readonly LimitedExponentialRetryInterval RetryIntervalForFileOperations = new LimitedExponentialRetryInterval(100, 200, 2);

        public static readonly ReadOnlyCollection<Encoding> DefaultInputEncodingPrecedence;

        static CalamariPhysicalFileSystem()
        {
#if NETSTANDARD
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required to use code pages in .NET Standard
#endif
            DefaultInputEncodingPrecedence = new List<Encoding>
            {
                new UTF8Encoding(false, true),
                Encoding.GetEncoding("windows-1252",
                                     EncoderFallback.ExceptionFallback /* Detect problems if re-used for output */,
                                     DecoderFallback.ReplacementFallback)
            }.AsReadOnly();
        }

        protected IFile File { get; set; } = new StandardFile();
        protected IDirectory Directory { get; set; } = new StandardDirectory();

        public static CalamariPhysicalFileSystem GetPhysicalFileSystem()
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                return new NixCalamariPhysicalFileSystem();

            return new WindowsPhysicalFileSystem();
        }

        /// <summary>
        /// For file operations, retry constantly up to one minute
        /// </summary>
        /// <remarks>
        /// Windows services can hang on to files for ~30s after the service has stopped as background
        /// threads shutdown or are killed for not shutting down in a timely fashion
        /// </remarks>
        protected static RetryTracker GetFileOperationRetryTracker()
        {
            return new RetryTracker(10000,
                TimeSpan.FromMinutes(1),
                RetryIntervalForFileOperations);
        }

        public bool FileExists(string? path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string? path)
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

        public virtual void DeleteFile(string path, FailureOptions options = FailureOptions.ThrowOnFailure)
        {
            DeleteFile(path, options, GetFileOperationRetryTracker(), CancellationToken.None);
        }

        void DeleteFile(string path, FailureOptions options, RetryTracker retry, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            retry.Reset();
            while (retry.Try())
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        if (retry.IsNotFirstAttempt)
                            File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }

                    break;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                            Log.VerboseFormat("Retry #{0} on delete file '{1}'. Exception: {2}", retry.CurrentTry, path, ex.Message);
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        if (options == FailureOptions.ThrowOnFailure)
                            throw;
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

            var retry = GetFileOperationRetryTracker();
            while (retry.Try())
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
                            Log.VerboseFormat("Retry #{0} on delete directory '{1}'. Exception: {2}", retry.CurrentTry, path, ex.Message);
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        if (options == FailureOptions.ThrowOnFailure)
                            throw;
                    }
                }
        }

        void EnsureDirectoryDeleted(string path, FailureOptions failureOptions)
        {
            var retry = GetFileOperationRetryTracker();

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

        IEnumerable<FileSystemInfo> EnumerateWithGlob(string parentDirectoryPath, params string[] globPattern)
        {
            var results = globPattern.Length == 0
                ? Glob.Expand(Path.Combine(parentDirectoryPath, "*"))
                : globPattern.SelectMany(pattern => Glob.Expand(Path.Combine(parentDirectoryPath, pattern)));

            return results
                .GroupBy(fi => fi.FullName) // use groupby + first to do .Distinct using fullname
                .Select(g => g.First());
        }

        public virtual IEnumerable<string> EnumerateFiles(
            string parentDirectoryPath,
            params string[] searchPatterns)
        {
            return Directory.EnumerateFiles(parentDirectoryPath, searchPatterns);
        }

        public virtual IEnumerable<string> EnumerateFilesRecursively(
            string parentDirectoryPath,
            params string[] searchPatterns)
        {
            return Directory.EnumerateFilesRecursively(parentDirectoryPath, searchPatterns);
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
            return ReadFile(path, out _);
        }

        public string ReadFile(string path, out Encoding encoding)
        {
            return ReadAllText(ReadAllBytes(path), out encoding, DefaultInputEncodingPrecedence);
        }

        public string ReadAllText(byte[] bytes, out Encoding encoding, ICollection<Encoding> encodingPrecedence)
        {
            if (encodingPrecedence.Count < 1)
                throw new Exception("No encodings specified.");

            if (encodingPrecedence.Take(encodingPrecedence.Count - 1)
                                  .FirstOrDefault(DecoderDoesNotRaiseErrorsForUnsupportedCharacters) is { } e)
                throw new Exception($"The supplied encoding '{e}' does not raise errors for unsupported characters, so the subsequent "
                                    + "encoder will never be used. Please set DecoderFallback to ExceptionFallback or use Unicode.");

            Exception? lastException = null;
            foreach (var encodingToTry in encodingPrecedence)
                try
                {
                    using (var stream = new MemoryStream(bytes))
                    using (var reader = new StreamReader(stream, encodingToTry))
                    {
                        var text = reader.ReadToEnd();
                        encoding = reader.CurrentEncoding;
                        return text;
                    }
                }
                catch (DecoderFallbackException ex)
                {
                    lastException = ex;
                }

            throw new Exception("Unable to decode file contents with the specified encodings.", lastException);
        }

        public static bool DecoderDoesNotRaiseErrorsForUnsupportedCharacters(Encoding encoding)
        {
            return encoding.DecoderFallback != DecoderFallback.ExceptionFallback
                   && !encoding.WebName.StartsWith("utf-")
                   && !encoding.WebName.StartsWith("unicode")
                   && !encoding.WebName.StartsWith("ucs-");
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void OverwriteFile(string path, string contents, Encoding? encoding = null)
        {
            RetryTrackerFileAction(() => WriteAllText(path, contents, encoding), path, "overwrite");
        }

        public void OverwriteFile(string path, Action<TextWriter> writeToWriter, Encoding? encoding = null)
        {
            RetryTrackerFileAction(() => WriteAllText(path, writeToWriter, encoding), path, "overwrite");
        }

        public void OverwriteFile(string path, byte[] data)
        {
            RetryTrackerFileAction(() => WriteAllBytes(path, data), path, "overwrite");
        }

        public void WriteAllText(string path, string contents, Encoding? encoding = null)
        {
            WriteAllText(path, writer => writer.Write(contents), encoding);
        }

        public void WriteAllText(string path, Action<TextWriter> writeToWriter, Encoding? encoding = null)
        {
            if (path.Length <= 0)
                throw new ArgumentException(path);

            var encodingsToTry = new List<Encoding> { new UTF8Encoding(false, true) };
            if (encoding != null)
                encodingsToTry.Insert(0, encoding);

            if (encodingsToTry.Take(encodingsToTry.Count - 1)
                              .FirstOrDefault(EncoderDoesNotRaiseErrorsForUnsupportedCharacters) is { } e)
                Log.Warn($"The supplied encoding '{e}' does not raise errors for unsupported characters, so the subsequent "
                         + "encoder will never be used. Please set DecoderFallback to ExceptionFallback or use Unicode.");

            byte[]? bytes = null;
            (Encoding encoding, Exception exception)? lastFailure = null;
            foreach (var currentEncoding in encodingsToTry)
            {
                if (lastFailure != null)
                    Log.Warn($"Unable to represent the output with encoding {lastFailure?.encoding.WebName}. Trying next the alternative: {currentEncoding.WebName}.");
                try
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var textWriter = new StreamWriter(memoryStream, currentEncoding))
                        {
                            writeToWriter(textWriter);
                        }

                        bytes = memoryStream.ToArray();
                    }

                    break;
                }
                catch (EncoderFallbackException ex)
                {
                    lastFailure = (currentEncoding, ex);
                }
            }

            if (bytes == null)
            {
                throw new Exception("Unable to encode text with the specified encodings.", lastFailure?.exception);
            }

            WriteAllBytes(path, bytes);
        }

        public static bool EncoderDoesNotRaiseErrorsForUnsupportedCharacters(Encoding encoding)
        {
            return encoding.EncoderFallback != EncoderFallback.ExceptionFallback
                   && !encoding.WebName.StartsWith("utf-")
                   && !encoding.WebName.StartsWith("unicode")
                   && !encoding.WebName.StartsWith("ucs-");
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
            var path1 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
            path1 = Path.Combine(path1, Assembly.GetEntryAssembly()?.GetName().Name ?? "Octopus.Calamari");
            path1 = Path.Combine(path1, "Temp");
            var path = path1;
            Directory.CreateDirectory(path);
            return Path.GetFullPath(path);
        }

        public string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public void CreateDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options)
        {
            PurgeDirectory(targetDirectory, fi => false, options);
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options, CancellationToken cancel)
        {
            PurgeDirectory(targetDirectory, fi => false, options, cancel);
        }

        public void PurgeDirectory(string targetDirectory, Predicate<FileSystemInfo> exclude, FailureOptions options)
        {
            PurgeDirectory(targetDirectory, exclude, options, CancellationToken.None);
        }

        public void PurgeDirectory(string targetDirectory, FailureOptions options, params string[] globs)
        {
            Predicate<FileSystemInfo>? check = null;
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

        void PurgeDirectory(string targetDirectory,
            Predicate<FileSystemInfo>? exclude,
            FailureOptions options,
            CancellationToken cancel,
            bool includeTarget = false,
            RetryTracker? retry = null)
        {
            exclude ??= (fi => false);

            if (!DirectoryExists(targetDirectory))
                return;

            retry ??= GetFileOperationRetryTracker();

            foreach (var file in EnumerateFiles(targetDirectory))
            {
                cancel.ThrowIfCancellationRequested();

                var includeInfo = new FileInfo(file);
                if (exclude(includeInfo))
                    continue;

                DeleteFile(file, options, retry, cancel);
            }

            foreach (var directory in EnumerateDirectories(targetDirectory))
            {
                cancel.ThrowIfCancellationRequested();

                var info = new DirectoryInfo(directory);
                if (exclude(info))
                    continue;

                if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    Directory.Delete(directory, false);
                else
                    PurgeDirectory(directory,
                        exclude,
                        options,
                        cancel,
                        true,
                        retry);
            }

            if (includeTarget && DirectoryIsEmpty(targetDirectory))
                DeleteDirectory(targetDirectory, options);
        }

        public void OverwriteAndDelete(string originalFile, string temporaryReplacement)
        {
            var backup = originalFile + ".backup" + Guid.NewGuid();

            try
            {
                if (!File.Exists(originalFile))
                    File.Copy(temporaryReplacement, originalFile, true);
                else
                    System.IO.File.Replace(temporaryReplacement, originalFile, backup);
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Log.VerboseFormat("Error attempting to copy or replace {0} with {1} Exception: {2}",
                                  originalFile,
                                  temporaryReplacement,
                                  unauthorizedAccessException.StackTrace);

                void LogFileAccess(string filePath)
                {
                    try
                    {
                        Log.VerboseFormat("Attempting to access with OpenOrCreate file mode, Read/Write Access and No file share {0}", filePath);
                        using (File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                        Log.VerboseFormat("Succeeded accessing {0}", filePath);
                    }
                    catch (Exception fileAccessException)
                    {
                        Log.VerboseFormat("Failed to access filePath: {0}, Exception: {1}", filePath, fileAccessException.ToString());
                    }
                }

                LogFileAccess(originalFile);
                LogFileAccess(temporaryReplacement);
                LogFileAccess(backup);

                throw unauthorizedAccessException;
            }

            File.Delete(temporaryReplacement);
            if (File.Exists(backup))
                File.Delete(backup);
        }

        // File.WriteAllBytes won't overwrite a hidden file, so implement our own.
        public void WriteAllBytes(string path, byte[] bytes)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Length <= 0)
                throw new ArgumentException(path);

            // FileMode.Open causes an existing file to be truncated to the
            // length of our new data, but can't overwrite a hidden file,
            // so use FileMode.OpenOrCreate and set the new file length manually.
            using (var fs = new FileStream(path, FileMode.OpenOrCreate))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
                fs.SetLength(fs.Position);
            }
        }

        public string RemoveInvalidFileNameChars(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            var invalidPathChars = Path.GetInvalidPathChars();
            var invalidFileChars = Path.GetInvalidFileNameChars();

            var result = new StringBuilder(path.Length);
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (!invalidPathChars.Contains(c) && !invalidFileChars.Contains(c))
                    result.Append(c);
            }

            return result.ToString();
        }

        public void MoveFile(string sourceFile, string destinationFile)
        {
             RetryTrackerFileAction(() => File.Move(sourceFile, destinationFile), destinationFile, "move");
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        } // ReSharper disable AssignNullToNotNullAttribute

        public int CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            return CopyDirectory(sourceDirectory, targetDirectory, CancellationToken.None);
        }

        public int CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel)
        {
            if (!DirectoryExists(sourceDirectory))
                return 0;

            if (!DirectoryExists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            var count = 0;
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

        static void RetryTrackerFileAction(Action fileAction, string target, string action)
        {
            var retry = GetFileOperationRetryTracker();
            while (retry.Try())
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
                            Log.VerboseFormat("Retry #{0} on {1} '{2}'. Exception: {3}",
                                retry.CurrentTry,
                                action,
                                target,
                                ex.Message);
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        throw;
                    }
                }
        }

        public string GetFullPath(string relativeOrAbsoluteFilePath)
        {
            if (!Path.IsPathRooted(relativeOrAbsoluteFilePath))
                relativeOrAbsoluteFilePath = Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsoluteFilePath);

            relativeOrAbsoluteFilePath = Path.GetFullPath(relativeOrAbsoluteFilePath);
            return relativeOrAbsoluteFilePath;
        }

        public abstract bool GetDiskFreeSpace(string directoryPath, out ulong totalNumberOfFreeBytes);
        public abstract bool GetDiskTotalSpace(string directoryPath, out ulong totalNumberOfBytes);

        public string GetRelativePath(string fromFile, string toFile)
        {
            var fromPathTokens = fromFile.Split(Path.DirectorySeparatorChar);
            var toPathTokens = toFile.Split(Path.DirectorySeparatorChar);

            var matchingTokens = 0;
            for (; matchingTokens < fromPathTokens.Count() - 1; matchingTokens++)
                if (!fromPathTokens[matchingTokens].Equals(toPathTokens[matchingTokens], StringComparison.Ordinal))
                    break;

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

        public string GetDirectoryName(string directoryPath)
        {
            return new DirectoryInfo(directoryPath).Name;
        }

        public Stream OpenFileExclusively(string filePath, FileMode fileMode, FileAccess fileAccess)
        {
            return File.Open(filePath, fileMode, fileAccess, FileShare.None);
        }
    }
}
