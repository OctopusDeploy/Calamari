#nullable enable
using System.Collections.Generic;


namespace Calamari.ArgoCD.Dtos
{
    public class ArgoCDCustomPropertiesDto
    {
        public ArgoCDCustomPropertiesDto(ArgoCDApplicationDto[] applications)
        {
            Applications = applications;
        }

        public ArgoCDApplicationDto[] Applications { get; set; }
    }

    public class ArgoCDApplicationDto
    {
        public ArgoCDApplicationDto(string gatewayId, string name, string defaultRegistry, ArgoCDApplicationSourceDto[] sources)
        {
            GatewayId = gatewayId;
            Name = name;
            DefaultRegistry = defaultRegistry;
            Sources = sources;
        }

        public string GatewayId { get; }
        public string Name { get; set; }
        public string DefaultRegistry { get; set; }
        public ArgoCDApplicationSourceDto[] Sources { get; set; }
    }

    public class ArgoCDApplicationSourceDto
    {
        public ArgoCDApplicationSourceDto(string url,
                                          string username,
                                          string password,
                                          string targetRevision,
                                          string path,
                                          string sourceType)
        {
            Url = url;
            Username = username;
            Password = password;
            TargetRevision = targetRevision;
            Path = path;
            SourceType = sourceType;
        }

        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string TargetRevision { get; set; }
        public string Path { get; set; }
        public string SourceType { get; set; }
    }
}
