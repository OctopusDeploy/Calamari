namespace Calamari.Integration.Nginx
{
    public class NixNginxServer : NginxServer
    {
        public override string GetConfigRootDirectory()
        {
            return "/etc/nginx/conf.d";
        }

        public override string GetSslRootDirectory()
        {
            return "/etc/ssl";
        }
    }
}
