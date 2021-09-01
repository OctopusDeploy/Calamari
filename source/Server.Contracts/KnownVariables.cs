using System;

namespace Sashimi.Server.Contracts
{
    public static class KnownVariables
    {
        public static readonly string UseRawScript = "OctopusUseRawScript";
        public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";

        public static class Project
        {
            public static readonly string Id = "Octopus.Project.Id";
        }

        public static class Account
        {
            public static readonly string AccountType = "Octopus.Account.AccountType";
        }

        public static class Environment
        {
            public static readonly string Id = "Octopus.Environment.Id";
        }

        public static class Deployment
        {
            public static readonly string Id = "Octopus.Deployment.Id";

            public static class Tenant
            {
                public static string Id = "Octopus.Deployment.Tenant.Id";
            }
        }

        public static class WorkerPool
        {
            public static readonly string Id = "Octopus.WorkerPool.Id";
        }

        public static class RunbookRun
        {
            public static readonly string Id = "Octopus.RunbookRun.Id";
        }

        public static class Certificate
        {
            public static string Name(string variableName)
            {
                return $"{variableName}.Name";
            }

            public static string Type(string variableName)
            {
                return $"{variableName}.Type";
            }

            public static string Thumbprint(string variableName)
            {
                return $"{variableName}.Thumbprint";
            }

            public static string RawOriginal(string variableName)
            {
                return $"{variableName}.RawOriginal";
            }

            public static string Password(string variableName)
            {
                return $"{variableName}.Password";
            }

            public static string Pfx(string variableName)
            {
                return $"{variableName}.Pfx";
            }

            public static string CertificateValue(string variableName)
            {
                return $"{variableName}.Certificate";
            }

            public static string SubjectCommonName(string variableName)
            {
                return $"{variableName}.SubjectCommonName";
            }

            public static string Issuer(string variableName)
            {
                return $"{variableName}.Issuer";
            }

            public static string NotBefore(string variableName)
            {
                return $"{variableName}.NotBefore";
            }

            public static string NotAfter(string variableName)
            {
                return $"{variableName}.NotAfter";
            }

            public static string CertificatePem(string variableName)
            {
                return $"{variableName}.CertificatePem";
            }

            public static string PrivateKey(string variableName)
            {
                return $"{variableName}.PrivateKey";
            }

            public static string PrivateKeyPem(string variableName)
            {
                return $"{variableName}.PrivateKeyPem";
            }

            public static string Subject(string variableName)
            {
                return $"{variableName}.Subject";
            }

            public static string ChainPem(string variableName)
            {
                return $"{variableName}.ChainPem";
            }
        }

        public static class Step
        {
            public static readonly string Id = "Octopus.Step.Id";
        }

        public static class Action
        {
            public static readonly string Id = "Octopus.Action.Id";
            public static readonly string EnabledFeatures = "Octopus.Action.EnabledFeatures";
            public static readonly string UseBundledTooling = "OctopusUseBundledTooling";

            public static class Container
            {
                public static readonly string Image = "Octopus.Action.Container.Image";
            }

            public static class Script
            {
                public static readonly string Syntax = "Octopus.Action.Script.Syntax";
                public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
                public static readonly string ScriptFileName = "Octopus.Action.Script.ScriptFileName";
                public static readonly string ScriptParameters = "Octopus.Action.Script.ScriptParameters";
                public static readonly string ScriptSource = "Octopus.Action.Script.ScriptSource";

                public static string ScriptBodyBySyntax(ScriptSyntax syntax)
                {
                    return $"Octopus.Action.Script.ScriptBody[{syntax.ToString()}]";
                }

                public static class ScriptSourceOptions
                {
                    public const string Package = "Package";
                    public const string Inline = "Inline";
                }
            }

            public static class Packages
            {
                public static readonly string PackageId = "Octopus.Action.Package.PackageId";
                public static readonly string NuGetPackageId = "Octopus.Action.Package.NuGetPackageId";

                public static readonly string FeedId = "Octopus.Action.Package.FeedId";
                public static readonly string NuGetFeedId = "Octopus.Action.Package.NuGetFeedId";

                public static readonly string Version = "Octopus.Action.Package.PackageVersion";

                public static string FeedIdForPackage(string name)
                {
                    return $"Octopus.Action.Package[{name}].FeedId";
                }

                public static string ImageForPackage(string name)
                {
                    return $"Octopus.Action.Package[{name}].Image";
                }

                public static string RegistryForPackage(string name)
                {
                    return $"Octopus.Action.Package[{name}].Registry";
                }

                public static string FeedUserNameForPackage(string name)
                {
                    return $"Octopus.Action.Package[{name}].Feed.UserName";
                }

                public static string FeedPasswordForPackage(string name)
                {
                    return $"Octopus.Action.Package[{name}].Feed.Password";
                }
            }

            public static class SubstituteInFiles
            {
                public static readonly string Targets = "Octopus.Action.SubstituteInFiles.TargetFiles";
                public static readonly string EnableNoMatchWarning = "Octopus.Action.SubstituteInFiles.EnableNoMatchWarning";
            }
        }

        public static class Features
        {
            public const string CustomScripts = "Octopus.Features.CustomScripts";
            public const string ConfigurationVariables = "Octopus.Features.ConfigurationVariables";
            public const string ConfigurationTransforms = "Octopus.Features.ConfigurationTransforms";
            public const string SubstituteInFiles = "Octopus.Features.SubstituteInFiles";
            public const string StructuredConfigurationVariables = "Octopus.Features.JsonConfigurationVariables";
        }
    }
}