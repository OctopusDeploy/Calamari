using Calamari.Util;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class RelativeGlobFixture
    {
        [Test]
        public void HasCorrectRelativePathsForRootGlobAll()
        {
            var files = new List<string>{ @"c:\staging\content\first.txt", @"c:\staging\content\nested\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("**/*").ToList();
            Assert.AreEqual(@"content/first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual( @"content/nested/two.txt", matches[1].MappedRelativePath);
        }
        
        [Test]
        public void HasCorrectRelativePathsForFolderGlob()
        {
            var files = new List<string>{ @"c:\staging\content\first.txt", @"c:\staging\content\nested\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("content/**/*").ToList();
            Assert.AreEqual(@"first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual(@"nested/two.txt", matches[1].MappedRelativePath);
        }
        
        [Test]
        public void HasCorrectRelativePathsForRootGlobAllAndOverride()
        {
            var files = new List<string>{ @"c:\staging\content\first.txt", @"c:\staging\content\nested\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("**/* => bob").ToList();
            Assert.AreEqual(@"bob/content/first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual(@"bob/content/nested/two.txt", matches[1].MappedRelativePath);
        }
        
        [Test]
        public void HasCorrectRelativePathsForFolderGlobAndOverride()
        {
            var files = new List<string>{ @"c:\staging\content\first.txt", @"c:\staging\content\nested\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("content/**/* => bob").ToList();
            Assert.AreEqual(@"bob/first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual(@"bob/nested/two.txt", matches[1].MappedRelativePath);
        }
        
        [Test]
        public void CanFlattenUsingOverride()
        {
            var files = new List<string>{ @"c:\staging\content\first.txt", @"c:\staging\content\nested\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("content/**/* => bob/*").ToList();
            Assert.AreEqual(@"bob/first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual(@"bob/two.txt", matches[1].MappedRelativePath);
        }
        
        [Test]
        public void DropsAllBaseFoldersForOverride()
        {
            var files = new List<string>{ @"c:\staging\content\nested\deep\first.txt", @"c:\staging\content\nested\deep\deeper\two.txt" };
            var matches = new RelativeGlobber((@base, pattern) => files, @"c:\staging").EnumerateFilesWithGlob("content/nested/**/* => bob").ToList();
            Assert.AreEqual(@"bob/deep/first.txt", matches[0].MappedRelativePath);
            Assert.AreEqual(@"bob/deep/deeper/two.txt", matches[1].MappedRelativePath);
        }
    }
}