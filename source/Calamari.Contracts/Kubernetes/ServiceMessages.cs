using System;

namespace Octopus.Calamari.Contracts.Kubernetes;

public static class ServiceMessages
{
    public static class ResourceStatus
    {
        public const string Name = "k8s-status";

        public static class Attributes
        {
            public const string Type = "type";
            public const string ActionId = "actionId";
            public const string StepName = "stepName";
            public const string TaskId = "taskId";
            public const string TargetId = "targetId";
            public const string TargetName = "targetName";
            public const string SpaceId = "spaceId";
            public const string Uuid = "uuid";
            public const string Group = "group";
            public const string Version = "version";
            public const string Kind = "kind";
            public const string Name = "name";
            public const string Namespace = "namespace";
            public const string Status = "status";
            public const string Data = "data";
            public const string Removed = "removed";
            public const string CheckCount = "checkCount";
        }
    }

    public static class ManifestApplied
    {
        public const string Name = "k8s-manifest-applied";
        public const string ManifestAttribute = "manifest";
        public const string NamespaceAttribute = "ns";
    }
}