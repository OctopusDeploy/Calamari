using Calamari.ArgoCD.Git;

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
        public ArgoCDApplicationDto(string gatewayId, string name, ArgoCDApplicationSourceDto[] sources)
        {
            GatewayId = gatewayId;
            Name = name;
            Sources = sources;
        }

        public string GatewayId { get; }
        public string Name { get; set; }
        public ArgoCDApplicationSourceDto[] Sources { get; set; }
    }

    public class ArgoCDApplicationSourceDto
    {
        public ArgoCDApplicationSourceDto(string url, 
                                        string username, 
                                        string password, 
                                        string targetRevision,
                                        string path)
        {
            Url = url;
            Username = username;
            Password = password;
            TargetRevision = targetRevision;
            Path = path;
        }

        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string TargetRevision { get; set; }
        public string Path { get; set; }
    }
}