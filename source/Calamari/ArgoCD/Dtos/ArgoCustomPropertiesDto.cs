#nullable enable
namespace Calamari.ArgoCD.Dtos
{
    public class ArgoCustomPropertiesDto
    {
        public ArgoCustomPropertiesDto(ArgoApplicationDto[] applications)
        {
            Applications = applications;
        }

        public ArgoApplicationDto[] Applications { get; set; }
    }
    
    public class ArgoApplicationDto
    {
        public ArgoApplicationDto(string name, ArgoApplicationSourceDto[] sources)
        {
            Name = name;
            Sources = sources;
        }

        public string Name { get; set; }
        public ArgoApplicationSourceDto[] Sources { get; set; }
    }

    public class ArgoApplicationSourceDto
    {
        public ArgoApplicationSourceDto(string url, 
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