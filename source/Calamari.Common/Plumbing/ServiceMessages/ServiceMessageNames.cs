using System;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    public class ServiceMessageNames
    {
        public static class SetVariable
        {
            public const string Name = "setVariable";
            public const string NameAttribute = "name";
            public const string ValueAttribute = "value";
        }

        public static class CalamariFoundPackage
        {
            public const string Name = "calamari-found-package";
        }

        public static class FoundPackage
        {
            public const string Name = "foundPackage";
            public const string IdAttribute = "id";
            public const string VersionAttribute = "version";
            public const string VersionFormat = "versionFormat";
            public const string HashAttribute = "hash";
            public const string RemotePathAttribute = "remotePath";
            public const string FileExtensionAttribute = "fileExtension";
        }

        public static class PackageDeltaVerification
        {
            public const string Name = "deltaVerification";
            public const string RemotePathAttribute = "remotePath";
            public const string HashAttribute = "hash";
            public const string SizeAttribute = "size";
            public const string Error = "error";
        }

        public static class CalamariTimedOperation
        {
            public const string Name = "calamari-timed-operation";
            public const string NameAttribute = "name";
            public const string OperationIdAttribute = "operationId";
            public const string DurationMillisecondsAttribute = "durationMilliseconds";
            public const string OutcomeAttribute = "outcome";
        }

        public static class CalamariDeploymentMetric
        {
            public const string Name = "calamari-deployment-metric";
            public const string OperationIdAttribute = "operationId";
            public const string MetricAttribute = "metric";
            public const string ValueAttribute = "value";
        }
    }
}