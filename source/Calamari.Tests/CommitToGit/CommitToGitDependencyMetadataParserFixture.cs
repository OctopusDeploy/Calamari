using System.Linq;
using Calamari.CommitToGit;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
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
        CommitToGitDependencyMetadataParser sut;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment(new CalamariVariables());

            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));

            sut = new CommitToGitDependencyMetadataParser(fileSystem);
        }

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
            SetInputFileSources("[{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEquivalentTo(new[] { "MyPackage" });
        }

        [Test]
        public void ReferencedDependencyNames_WhenGitRepositoryType_ReturnsGitDependencyName()
        {
            SetInputFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}]");

            var result = sut.ReferencedDependencyNames(deployment);

            result.Should().BeEquivalentTo(new[] { "my-repo" });
        }

        [Test]
        public void ReferencedDependencyNames_WhenMixedTypes_ReturnsPackageAndGitNames()
        {
            SetInputFileSources("[" +
                                   "{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}," +
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
            SetInputFileSources("[{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[\"**/*\"],\"DestinationSubFolder\":\"output\"}]");

            var result = sut.GetPackageDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].PackageId.Should().Be("pkg-id");
            result[0].PackageName.Should().Be("MyPackage");
            result[0].InputFilePaths.Should().BeEquivalentTo("**/*" );
            result[0].DestinationSubFolder.Should().Be("output");
        }

        [Test]
        public void GetPackageDependenciesForCopying_WhenOnlyGitRepositoryType_ReturnsEmpty()
        {
            SetInputFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetPackageDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetPackageDependenciesForCopying_EvaluatesVariablesInPackageProperties()
        {
            deployment.Variables.Set("PackageIdVar", "resolved-pkg-id");
            deployment.Variables.Set("PackageNameVar", "resolved-pkg-name");
            SetInputFileSources("[{\"Type\":\"Package\",\"PackageId\":\"#{PackageIdVar}\",\"PackageName\":\"#{PackageNameVar}\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetPackageDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].PackageId.Should().Be("resolved-pkg-id");
            result[0].PackageName.Should().Be("resolved-pkg-name");
        }

        [Test]
        public void GetPackageDependenciesForCopying_WhenMultiplePackages_ReturnsAll()
        {
            SetInputFileSources("[" +
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
            SetInputFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"output\",\"InputFilePaths\":[\"src/**/*\",\"docs/**/*\"]}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].GitDependencyName.Should().Be("my-repo");
            result[0].DestinationSubFolder.Should().Be("output");
            result[0].InputFilePaths.Should().BeEquivalentTo("src/**/*", "docs/**/*");
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenInputFilePathsNotSpecified_DefaultsToWildcard()
        {
            SetInputFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"my-repo\",\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].InputFilePaths.Should().BeEquivalentTo("**/*");
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenOnlyPackageType_ReturnsEmpty()
        {
            SetInputFileSources("[{\"Type\":\"Package\",\"PackageId\":\"pkg-id\",\"PackageName\":\"MyPackage\",\"InputFilePaths\":[],\"DestinationSubFolder\":\"\"}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment);

            result.Should().BeEmpty();
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_EvaluatesVariablesInGitProperties()
        {
            deployment.Variables.Set("RepoNameVar", "resolved-repo");
            deployment.Variables.Set("PathVar", "src/app");
            SetInputFileSources("[{\"Type\":\"GitRepository\",\"GitDependencyName\":\"#{RepoNameVar}\",\"DestinationSubFolder\":\"\",\"InputFilePaths\":[\"#{PathVar}/**/*\"]}]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(1);
            result[0].GitDependencyName.Should().Be("resolved-repo");
            result[0].InputFilePaths.Should().BeEquivalentTo("src/app/**/*");
        }

        [Test]
        public void GetGitRepositoryDependenciesForCopying_WhenMultipleGitRepos_ReturnsAll()
        {
            SetInputFileSources("[" +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"repo-1\",\"DestinationSubFolder\":\"\"}," +
                                   "{\"Type\":\"GitRepository\",\"GitDependencyName\":\"repo-2\",\"DestinationSubFolder\":\"\"}" +
                                   "]");

            var result = sut.GetGitRepositoryDependenciesForCopying(deployment).ToList();

            result.Should().HaveCount(2);
            result.Select(r => r.GitDependencyName).Should().BeEquivalentTo(new[] { "repo-1", "repo-2" });
        }

        #endregion

        void SetInputFileSources(string json)
        {
            deployment.Variables.Set(Deployment.SpecialVariables.Action.Git.InputFileSources, json);
        }
    }
}
