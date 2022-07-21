
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationTransforms
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class TransformFileLocatorFixture
    {
        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Transform and target are in the same directory")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform("web.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Transform and multiple targets are in the same directory")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform("connstrings.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\connstrings.mytransform.config")
                .And.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\temp\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform from a different directory")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .When.UsingTransform(@"transforms\web.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform from a different directory against multiple files")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"transforms\connstrings.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config")
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Using an absolute path to the transform")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\subdir\web.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .When.UsingTransform(@"c:\transforms\web.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\subdir\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Using an absolute path to the transform, and applying it against multiple files")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\connstrings.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config")
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Using an absolute path to the transform with an absolute path to multiple files")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\connstrings.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple absolute path transforms to the same target file")
                .Given.FileExists(@"c:\temp\web.config")
                .Given.FileExists(@"c:\temp\subdir\web.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .And.FileExists(@"c:\transforms\security.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config", @"c:\transforms\security.mytransform.config")
                .Then.SourceFile(@"c:\temp\subdir\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config", @"c:\transforms\security.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Using an absolute path wildcard transform and multiple targets")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\subdir\web.config")
                .And.FileExists(@"c:\temp\subdir\app.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .And.FileExists(@"c:\transforms\app.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\transforms\app.mytransform.config")
                .Then.SourceFile(@"c:\temp\subdir\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\subdir\app.config")
                .Should.BeTransFormedBy(@"c:\transforms\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsRelativePath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Using an absolute path to a transform against a target in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\transforms\security.mytransform.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Using an absolute path wildcard transform against an absolute path target")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\transforms\security.mytransform.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Using an absolute path wildcard transform against an absolute path wildcard target")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\transforms\security.mytransform.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFullPath_And_TargetIsWildcardRelativePath_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Using an absolute path for multiple transforms against multiple relative files")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .And.FileExists(@"c:\transforms\app.mytransform.config")
                .When.UsingTransform(@"c:\transforms\*.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\transforms\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple relative transforms against a specific target")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\security.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config", @"c:\temp\transforms\security.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying transforms from a different directory to multiple targets")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\app.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsRelativePath_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple transforms in a different directory to a single target in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\security.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config", @"c:\temp\transforms\security.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsWildcardRelativePath_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying transforms from a different directory to targets in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\app.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying multiple transforms in a different directory to multiple targets with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\transforms\security.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying multiple transforms to multiple targets with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .Given.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\security.mytransform.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardRelativePath_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying multiple transforms in a different directory to a target with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\transforms\security.mytransform.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"transforms\*.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple transforms to a single target where both are in the same directory")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\security.mytransform.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\security.mytransform.config", @"c:\temp\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsFileNameOnly_2_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Wildcard transform with wildcard in the middle of the filename to a single target where both are in the same directory")
                .Given.FileExists(@"c:\temp\MyApp.connstrings.octopus.config")
                .And.FileExists(@"c:\temp\MyApp.nlog_octopus.config")
                .And.FileExists(@"c:\temp\MyApp.WinSvc.exe.config")
                .When.UsingTransform(@"MyApp.*.octopus.config => MyApp.WinSvc.exe.config")
                .Then.SourceFile(@"c:\temp\MyApp.WinSvc.exe.config")
                .Should.BeTransFormedBy(@"c:\temp\MyApp.connstrings.octopus.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsWildCardInsideFileNameOnly_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Using wildcard in the middle of target filename")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => w*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsWildcardFileNameOnly_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple transforms against multiple targets")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .And.FileExists(@"c:\temp\app.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => *.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\web.mytransform.config")
                .And.SourceFile(@"c:\temp\app.config")
                .Should.BeTransFormedBy(@"c:\temp\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsRelativePath_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple transforms to a single target in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\security.mytransform.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\security.mytransform.config", @"c:\temp\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsWildcardRelativePath_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying multiple transforms against multiple targets in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\temp\app.mytransform.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\web.mytransform.config")
                .Then.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\temp\app.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsWildcardFileNameOnly_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying multiple transforms against a target with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\security.mytransform.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform(@"*.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsRelative_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform against a target in a different folder")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform(@"web.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsWildcardRelative_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform against multiple targets in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\temp\connstrings.mytransform.config")
                .When.UsingTransform(@"connstrings.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\connstrings.mytransform.config")
                .And.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\temp\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsRelative_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform to a target in a sibling directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .When.UsingTransform(@"transforms\web.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsRelative_AndDirectoryRepeats_ItSucceeds()
        {
            // https://github.com/OctopusDeploy/Issues/issues/3152
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform to a target in a sibling directory that repeats")
                .Given.FileExists(@"c:\temp\temp\web.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .When.UsingTransform(@"transforms\web.mytransform.config => temp\web.config")
                .Then.SourceFile(@"c:\temp\temp\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\web.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsWildcardRelative_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform to multiple targets in a sibling directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\temp\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"transforms\connstrings.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config")
                .And.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\temp\transforms\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsRelative_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform with an absolute path to target in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .When.UsingTransform(@"c:\transforms\web.mytransform.config => config\web.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsRelativeWildcard_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform with an absolute path against multiple files in a different directory")
                .Given.FileExists(@"c:\temp\config\web.config")
                .And.FileExists(@"c:\temp\config\app.config")
                .And.FileExists(@"c:\transforms\connstrings.mytransform.config")
                .When.UsingTransform(@"c:\transforms\connstrings.mytransform.config => config\*.config")
                .Then.SourceFile(@"c:\temp\config\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config")
                .And.SourceFile(@"c:\temp\config\app.config")
                .Should.BeTransFormedBy(@"c:\transforms\connstrings.mytransform.config")
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform against an absolute path target")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform(@"web.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform from a relative directory to an absolute path target")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .When.UsingTransform(@"transforms\web.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsRelativePath_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform from a relative directory to absolute path targets")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\transforms\web.mytransform.config")
                .When.UsingTransform(@"transforms\web.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFileNameOnly_And_TargetIsWildcardFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform against an multiple targets with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\app.config")
                .And.FileExists(@"c:\temp\web.mytransform.config")
                .When.UsingTransform(@"web.mytransform.config => c:\temp\*.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Then.SourceFile(@"c:\temp\app.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsFullPath_ItFails()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Not supported: Applying a transform with an absolute path to a target with an absolute path")
                .Given.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .When.UsingTransform(@"c:\transforms\web.mytransform.config => c:\temp\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsInTheExtractionDirectoryRoot_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform with an absolute path to a target in the extraction path root")
                .Given.ExtractionDirectoryIs(@"c:\temp")
                .And.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\subdir\web.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .When.UsingTransform(@"c:\transforms\web.mytransform.config => .\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .And.SourceFile(@"c:\temp\subdir\web.config")
                .Should.FailToBeTransformed()
                .Verify(this);
        }

        [Test]
        public void When_TransformIsFullPath_And_TargetIsRelativeToExtractionDirectory_ItSucceeds()
        {
            ConfigurationTransformTestCaseBuilder
                .ForTheScenario("Applying a transform with an absolute path to a target relative to the extraction path")
                .Given.ExtractionDirectoryIs(@"c:\temp")
                .And.FileExists(@"c:\temp\web.config")
                .And.FileExists(@"c:\temp\subdir\web.config")
                .And.FileExists(@"c:\transforms\web.mytransform.config")
                .When.UsingTransform(@"c:\transforms\web.mytransform.config => .\subdir\web.config")
                .Then.SourceFile(@"c:\temp\web.config")
                .Should.FailToBeTransformed()
                .And.SourceFile(@"c:\temp\subdir\web.config")
                .Should.BeTransFormedBy(@"c:\transforms\web.mytransform.config")
                .Verify(this);
        }
    }
}
