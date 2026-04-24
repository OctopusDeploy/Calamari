using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.CommitToGit;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.CommitToGit
{
    [TestFixture]
    public class CommitToGitDependencyMetadataParserFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        InMemoryLog log;
        CommitToGitDependencyMetadataParser sut;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment(new CalamariVariables());

            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));

            log = new InMemoryLog();
            sut = new CommitToGitDependencyMetadataParser(fileSystem, log);
        }

        [Test]
        public void blah()
        {
            var value = """[{"Type":"Package","PackageId":"first-1","PackageName":"first-1","InputFilePaths":"","DestinationSubfolder":"./"}]""";
            deployment.Variables.Set(Deployment.SpecialVariables.Action.Git.TemplateFileSources, value);

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEmpty();
        }
        
        #region ParseInputFilesFromDependencies

        [Test]
        public void ParseInputFilesFromDependencies_WhenTemplateFileSourcesIsNull_ReturnsEmpty()
        {
            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEmpty();
        }

        [TestCase("")]
        [TestCase("  ")]
        [TestCase("\t")]
        public void ParseInputFilesFromDependencies_WhenTemplateFileSourcesIsWhitespace_ReturnsEmpty(string value)
        {
            deployment.Variables.Set(Deployment.SpecialVariables.Action.Git.TemplateFileSources, value);

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void ParseInputFilesFromDependencies_WhenOnlyInlineType_ReturnsEmpty()
        {
            SetTemplateFileSources("[{\"Type\":\"Inline\",\"FileContent\":\"key: value\",\"DestinationFilename\":\"inline.yaml\"}]");

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void ParseInputFilesFromDependencies_WhenPackageTypeWithMatchingFiles_ReturnsFiles()
        {
            const string packageName = "MyPackage";
            const string packageId = "pkg-id";
            const string valuesFile = "values.yaml";

            SetTemplateFileSources($"[{{\"Type\":\"Package\",\"PackageId\":\"{packageId}\",\"PackageName\":\"{packageName}\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"{valuesFile}\"}}]");
            deployment.Variables.Add(PackageVariables.IndexedPackageId(packageName), packageId);
            SetupFileSystemToReturnFiles(packageName, valuesFile);

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEquivalentTo(new[] { Path.Combine(deployment.CurrentDirectory, packageName, valuesFile) });
        }

        [Test]
        public void ParseInputFilesFromDependencies_WhenGitRepositoryTypeWithMatchingFiles_ReturnsFiles()
        {
            const string gitDepName = "my-git-dep";
            const string valuesFile = "values.yaml";

            SetTemplateFileSources($"[{{\"Type\":\"GitRepository\",\"GitDependencyName\":\"{gitDepName}\",\"DestinationSubFolder\":\"{valuesFile}\"}}]");
            deployment.Variables.Add(Deployment.SpecialVariables.GitResources.CommitHash(gitDepName), "abc123");
            SetupFileSystemToReturnFiles(gitDepName, valuesFile);

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEquivalentTo(new[] { Path.Combine(deployment.CurrentDirectory, gitDepName, valuesFile) });
        }

        [Test]
        public void ParseInputFilesFromDependencies_WhenPackageWriterReturnsNull_ExcludesFromResult()
        {
            // Package with no matching variables — FindPackageValuesFiles returns null
            const string packageName = "UnknownPackage";
            SetTemplateFileSources($"[{{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"{packageName}\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"values.yaml\"}}]");
            // Deliberately NOT adding package variables so the writer returns null

            var result = sut.ParseInputFilesFromDependencies(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void ParseInputFilesFromDependencies_WhenMultipleSources_TopSourceAppearsLastForHigherPrecedence()
        {
            const string packageId1 = "pkg-1";
            const string packageName1 = "Package1";
            const string valuesFile1 = "values1.yaml";

            const string packageId2 = "pkg-2";
            const string packageName2 = "Package2";
            const string valuesFile2 = "values2.yaml";

            // Package1 is listed first (index 0 = higher precedence in Helm --values args)
            SetTemplateFileSources($"[" +
                                   $"{{\"Type\":\"Package\",\"PackageId\":\"{packageId1}\",\"PackageName\":\"{packageName1}\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"{valuesFile1}\"}}," +
                                   $"{{\"Type\":\"Package\",\"PackageId\":\"{packageId2}\",\"PackageName\":\"{packageName2}\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"{valuesFile2}\"}}" +
                                   $"]");

            deployment.Variables.Add(PackageVariables.IndexedPackageId(packageName1), packageId1);
            deployment.Variables.Add(PackageVariables.IndexedPackageId(packageName2), packageId2);
            SetupFileSystemToReturnFiles(packageName1, valuesFile1);
            SetupFileSystemToReturnFiles(packageName2, valuesFile2);

            var result = sut.ParseInputFilesFromDependencies(deployment).ToList();

            // Package2 (index 1) appears first, Package1 (index 0) appears last — so Package1 has higher precedence
            result.Should().HaveCount(2);
            result[0].Should().Contain(valuesFile2);
            result[1].Should().Contain(valuesFile1);
        }

        #endregion

        #region ReferencedDependencyNames

        [Test]
        public void ReferencedDependencyNames_WhenTemplateFileSourcesIsNull_ReturnsEmpty()
        {
            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void ReferencedDependencyNames_WhenPackageType_ReturnsPackageName()
        {
            SetTemplateFileSources("[{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEquivalentTo(new[] { "MyPackage" });
        }

        [Test]
        public void ReferencedDependencyNames_WhenGitRepositoryType_ReturnsGitDependencyName()
        {
            SetTemplateFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEquivalentTo(new[] { "my-repo" });
        }

        [Test]
        public void ReferencedDependencyNames_WhenInlineType_ExcludesFromResult()
        {
            SetTemplateFileSources("[{\"Type\":\"Inline\",\"FileContent\":\"key: value\",\"DestinationFilename\":\"inline.yaml\"}]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void ReferencedDependencyNames_WhenMixedTypes_ReturnsOnlyPackageAndGitNames()
        {
            SetTemplateFileSources("[" +
                                   "{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"Inline\",\"FileContent\":\"key: value\",\"DestinationFilename\":\"inline.yaml\"}," +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}" +
                                   "]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEquivalentTo(new[] { "MyPackage", "my-repo" });
        }

        #endregion

        #region GetPackageDependenciesForCopying

        [Test]
        public void GetPackageDependenciesForCopying_WhenTemplateFileSourcesIsNull_ReturnsEmpty()
        {
            var result = sut.GetPackageDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetPackageDependenciesForCopying_WhenPackageType_ReturnsDependencyWithCorrectFields()
        {
            SetTemplateFileSources("[{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"output\"}]");

            var result = sut.GetPackageDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].PackageId.Should().Be("pkg-id");
            result[0].PackageName.Should().Be("MyPackage");
            result[0].InputFilePaths.Should().BeEquivalentTo("**/*" );
            result[0].DestinationSubFolder.Should().Be("output");
        }

        [Test]
        public void GetPackageDependenciesForCopying_WhenGitRepositoryAndInlineTypes_ReturnsEmpty()
        {
            SetTemplateFileSources("[" +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"Inline\",\"FileContent\":\"key: value\",\"DestinationFilename\":\"inline.yaml\"}" +
                                   "]");

            var result = sut.GetPackageDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetPackageDependenciesForCopying_EvaluatesVariablesInPackageProperties()
        {
            deployment.Variables.Set("PackageIdVar", "resolved-pkg-id");
            deployment.Variables.Set("PackageNameVar", "resolved-pkg-name");
            SetTemplateFileSources("[{\"Type\":\"Package\",\"PackageId\":\"#{PackageIdVar}\",\"PackageName\":\"#{PackageNameVar}\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetPackageDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].PackageId.Should().Be("resolved-pkg-id");
            result[0].PackageName.Should().Be("resolved-pkg-name");
        }

        [Test]
        public void GetPackageDependenciesForCopying_WhenMultiplePackages_ReturnsAll()
        {
            SetTemplateFileSources("[" +
                                   "{\"Type\":\"Package\",\"PackageId\":\"pkg-1\",\"PackageName\":\"Package1\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"Package\",\"PackageId\":\"pkg-2\",\"PackageName\":\"Package2\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}" +
                                   "]");

            var result = sut.GetPackageDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(2);
            result.Select(p => p.PackageName).Should().BeEquivalentTo(new[] { "Package1", "Package2" });
        }

        #endregion

        #region GetGitRepositoryDependenciesForCopying

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenTemplateFileSourcesIsNull_ReturnsEmpty()
        {
            var result = sut.GetGitRepositoryDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenGitRepositoryType_ReturnsDependencyWithCorrectFields()
        {
            SetTemplateFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"output\"}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].GitDependencyName.Should().Be("my-repo");
            result[0].DestinationSubFolder.Should().Be("output");
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenPackageAndInlineTypes_ReturnsEmpty()
        {
            SetTemplateFileSources("[" +
                                   "{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"Inline\",\"FileContent\":\"key: value\",\"DestinationFilename\":\"inline.yaml\"}" +
                                   "]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_EvaluatesVariablesInGitProperties()
        {
            deployment.Variables.Set("RepoNameVar", "resolved-repo");
            SetTemplateFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"#{RepoNameVar}\",\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].GitDependencyName.Should().Be("resolved-repo");
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenMultipleGitRepos_ReturnsAll()
        {
            SetTemplateFileSources("[" +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"repo-1\",\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"repo-2\",\"DestinationSubFolder\":\"\"}" +
                                   "]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(2);
            result.Select(r => r.GitDependencyName).Should().BeEquivalentTo(new[] { "repo-1", "repo-2" });
        }

        #endregion

        void SetTemplateFileSources(string json)
        {
            deployment.Variables.Set(Deployment.SpecialVariables.Action.Git.TemplateFileSources, json);
        }

        void SetupFileSystemToReturnFiles(string subFolder, params string[] filenames)
        {
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Is<string[]>(paths => paths.Any(p => p.Contains(subFolder) || filenames.Any(f => p.Contains(f)))))
                      .Returns(ci => filenames.Select(f => Path.Combine(ci.ArgAt<string>(0), subFolder, f)).ToList());
        }
    }
}
