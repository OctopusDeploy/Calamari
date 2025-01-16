// This file will be updated by our build process at compile time.
using System;
using System.Reflection;

[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyFileVersion("0.0.0.0")]
[assembly: AssemblyInformationalVersion("0.0.0-local")]
[assembly: AssemblyGitBranch("UNKNOWNBRANCH")]
[assembly: AssemblyNuGetVersion("0.0.0-local")]

#if DEFINE_VERSION_ATTRIBUTES
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class AssemblyGitBranchAttribute : Attribute
    {
        public AssemblyGitBranchAttribute(string branchName)
        {
            BranchName = branchName;
        }

        public string BranchName { get; }
    }

    public sealed class AssemblyNuGetVersionAttribute : Attribute
    {
        public AssemblyNuGetVersionAttribute(string nuGetVersion)
        {
            NuGetVersion = nuGetVersion;
        }

        public string NuGetVersion { get; }
    }
#endif