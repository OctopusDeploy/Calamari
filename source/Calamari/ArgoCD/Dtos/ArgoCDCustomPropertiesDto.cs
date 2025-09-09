namespace Calamari.ArgoCD.Dtos
{
    public class ArgoCDCustomPropertiesDto
    {
        public ArgoCDCustomPropertiesDto(ArgoCDApplicationDto[] applications, GitCredentialDto[] credentials)
        {
            Applications = applications;
            Credentials = credentials;
        }

        public ArgoCDApplicationDto[] Applications { get; }
        public GitCredentialDto[] Credentials { get; }
    }
    
    public class ArgoCDApplicationDto
    {
        public ArgoCDApplicationDto(string gatewayId, string name, ArgoCDApplicationSourceDto[] sources, string manifest)
        {
            GatewayId = gatewayId;
            Name = name;
            Sources = sources;
            Manifest = manifest;
        }

        public string GatewayId { get; }
        public string Name { get; } 
        public ArgoCDApplicationSourceDto[] Sources { get; }
        public string Manifest { get; }
    }

    public class ArgoCDApplicationSourceDto
    {
        public ArgoCDApplicationSourceDto(string url, string path, string targetRevision)
        {
            Url = url;
            Path = path;
            TargetRevision = targetRevision;
        }

        public string Url { get; }
        public string TargetRevision { get; }
        public string Path { get; }
    }

    public class GitCredentialDto
    {
        public GitCredentialDto(string url, string username, string password)
        {
            Url = url;
            Username = username;
            Password = password;
        }

        public string Url { get; }
        public string Username { get; }
        public string Password { get; }
    }
}