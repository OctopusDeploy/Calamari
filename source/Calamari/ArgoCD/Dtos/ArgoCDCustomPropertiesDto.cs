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
        public ArgoCDApplicationDto(string name, ArgoCDApplicationSourceDto[] sources)
        {
            Name = name;
            Sources = sources;
        }

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