using System;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageExtractorUtilsFixture
    {
        static string Root => CalamariEnvironment.IsRunningOnWindows ? @"C:\Octopus\Work" : "/tmp/Octopus/Work";

        static void Guard(string entryKey, string extractionDirectory)
            => PackageExtractorUtils.ThrowIfPathTraversalAttempted(entryKey, extractionDirectory);

        /// <summary>
        /// Path.GetFullPath never changes the case of a path component, so for an ordinary entry the root prefix of
        /// the resolved destination is byte-identical to the extraction root - which is why making the comparison
        /// case-sensitive cannot reject archives that used to extract fine.
        /// </summary>
        [Test]
        [TestCase("file.txt")]
        [TestCase("folder/file.txt")]
        [TestCase("deeply/nested/folder/file.txt")]
        [TestCase("folder/")]
        [TestCase("safe/../file.txt")]
        [TestCase("a/b/../../c/file.txt")]
        [TestCase("Mixed/CASE/File.TXT")]
        [TestCase("file with spaces.txt")]
        [TestCase("unicode-\u00fc\u00f1\u00ee/file.txt")]
        [TestCase("./")]
        [TestCase("./nested/file.txt", Description = "Leading ./ is what GNU tar emits for `tar -cf x.tar .`")]
        public void AllowsEntriesInsideTheExtractionRoot(string entryKey)
        {
            Assert.DoesNotThrow(() => Guard(entryKey, Root));
        }

        [Test]
        [TestCase("../escaped.txt")]
        [TestCase("../../escaped.txt")]
        [TestCase("folder/../../escaped.txt")]
        [TestCase("../Work-evil/escaped.txt", Description = "Sibling directory sharing the root as a string prefix")]
        [TestCase("../../../../../../../../etc/passwd")]
        public void ThrowsOnEntriesThatEscapeTheExtractionRoot(string entryKey)
        {
            Assert.Throws<InvalidOperationException>(() => Guard(entryKey, Root));
        }

        [Test]
        public void ThrowsOnAbsoluteEntryKeys()
        {
            var absolute = CalamariEnvironment.IsRunningOnWindows ? @"C:\Windows\evil.txt" : "/etc/evil.txt";

            Assert.Throws<InvalidOperationException>(() => Guard(absolute, Root));
        }

        /// <summary>
        /// The behaviour this change exists for. An entry that leaves the root and re-enters a directory differing
        /// only in case is a genuine escape on a case-sensitive filesystem, and the very same directory on a
        /// case-insensitive one - so the correct verdict differs by platform, not by taste. Each CI leg covers its
        /// own branch: the throwing case on Linux, the non-throwing case on Windows and macOS.
        /// </summary>
        [Test]
        public void CaseVariantReEntryFollowsPlatformCaseSensitivity()
        {
            var entryKey = CalamariEnvironment.IsRunningOnWindows
                ? @"..\..\OCTOPUS\WORK\evil.txt"
                : "../../Octopus/WORK/evil.txt";

            if (CalamariEnvironment.IsRunningOnNix)
                Assert.Throws<InvalidOperationException>(() => Guard(entryKey, Root),
                                                        "On a case-sensitive filesystem a case-variant path is a different directory, so this escapes the root.");
            else
                Assert.DoesNotThrow(() => Guard(entryKey, Root),
                                    "On a case-insensitive filesystem a case-variant path is the same directory, so this stays inside the root.");
        }

        [Test]
        public void HandlesAnExtractionRootThatAlreadyEndsInASeparator()
        {
            var rootWithSeparator = Root + Path.DirectorySeparatorChar;

            Assert.DoesNotThrow(() => Guard("file.txt", rootWithSeparator));
            Assert.Throws<InvalidOperationException>(() => Guard("../escaped.txt", rootWithSeparator));
        }

        [Test]
        public void HandlesRelativeExtractionDirectories()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), tempFolder.DirectoryPath);

            Assert.DoesNotThrow(() => Guard("file.txt", relative));
            Assert.Throws<InvalidOperationException>(() => Guard("../../../../../../../../escaped.txt", relative));
        }
    }
}
